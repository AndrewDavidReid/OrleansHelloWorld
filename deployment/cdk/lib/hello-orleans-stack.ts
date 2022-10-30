import * as cdk from "aws-cdk-lib";
import { Construct } from "constructs";
import { Port, SecurityGroup, Vpc } from "aws-cdk-lib/aws-ec2";
import {
  AwsLogDriverMode,
  Cluster,
  Compatibility,
  ContainerDefinition,
  ContainerImage,
  FargateService,
  HealthCheck,
  LogDriver,
  NetworkMode,
  TaskDefinition,
} from "aws-cdk-lib/aws-ecs";
import { Duration } from "aws-cdk-lib";
import {
  ApplicationLoadBalancer,
  ApplicationProtocol,
  ApplicationTargetGroup,
  Protocol,
  TargetType,
} from "aws-cdk-lib/aws-elasticloadbalancingv2";
import { RetentionDays } from "aws-cdk-lib/aws-logs";

export class HelloOrleansStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);

    const vpc = new Vpc(this, "orleans-vpc", {
      maxAzs: 2,
    });

    const cluster = new Cluster(this, "ecs-cluster", {
      vpc: vpc,
    });

    const siloTaskDefinition = new TaskDefinition(this, "silo-task-def", {
      compatibility: Compatibility.FARGATE,
      networkMode: NetworkMode.AWS_VPC,
      cpu: "512",
      memoryMiB: "1024",
    });

    // TODO: Update site to return a randomly generated message, saved as a grain

    // HealthCheck
    const healthCheck: HealthCheck = {
      command: ["CMD-SHELL", "curl -f http://localhost/healthz || exit 1"],
    };

    const siloContainer = new ContainerDefinition(this, "silo-container-def", {
      taskDefinition: siloTaskDefinition,
      environment: {
        ["RUN_ON_AWS"]: "true",
        ["MONGO_CONNECTION_STRING"]: process.env.MONGO_CONNECTION_STRING ?? "",
      },
      healthCheck,
      image: ContainerImage.fromRegistry("peiandy/hello-silo:v3"),
      essential: true,
      logging: LogDriver.awsLogs({
        streamPrefix: "silo-",
        logRetention: RetentionDays.ONE_DAY,
        mode: AwsLogDriverMode.NON_BLOCKING,
      }),
      portMappings: [
        {
          containerPort: 11111,
        },
        {
          containerPort: 30000,
        },
        {
          containerPort: 80,
        },
        {
          containerPort: 8080,
        },
      ],
    });

    const siloSecurityGroup = new SecurityGroup(this, "silo-sg", {
      vpc,
    });

    const siloPeeringSecurityGroup = new SecurityGroup(
      this,
      "silo-peering-sg",
      {
        vpc,
      }
    );

    const albSecurityGroup = new SecurityGroup(this, "alb-sg", {
      vpc,
    });

    // ALB to dashboard.
    siloSecurityGroup.addIngressRule(albSecurityGroup, Port.tcp(80));
    siloSecurityGroup.addIngressRule(albSecurityGroup, Port.tcp(8080));
    siloPeeringSecurityGroup.addIngressRule(siloSecurityGroup, Port.tcp(11111));
    siloPeeringSecurityGroup.addEgressRule(siloSecurityGroup, Port.tcp(11111));

    const siloService = new FargateService(this, "silo-fargate-service", {
      cluster,
      desiredCount: 2,
      taskDefinition: siloTaskDefinition,
      healthCheckGracePeriod: Duration.seconds(180),
      securityGroups: [siloSecurityGroup, siloPeeringSecurityGroup],
    });

    const loadBalancer = new ApplicationLoadBalancer(
      this,
      "hello-orleans-alb",
      {
        vpc,
        securityGroup: albSecurityGroup,
        idleTimeout: Duration.seconds(180),
        internetFacing: true,
      }
    );

    const dashboardTargetGroup = new ApplicationTargetGroup(
      this,
      "dashboard-atg",
      {
        vpc,
        targetType: TargetType.IP,
        port: 8080,
        protocol: ApplicationProtocol.HTTP,
        healthCheck: {
          interval: Duration.seconds(30),
          path: "/",
          protocol: Protocol.HTTP,
          healthyThresholdCount: 2,
          unhealthyThresholdCount: 3,
        },
        targets: [
          siloService.loadBalancerTarget({
            containerName: siloContainer.containerName,
            containerPort: 8080,
          }),
        ],
      }
    );

    const dashboardListener = loadBalancer.addListener(
      "dashboard-http-listener",
      {
        port: 8080,
        protocol: ApplicationProtocol.HTTP,
        defaultTargetGroups: [dashboardTargetGroup],
        open: true,
      }
    );

    const siloTargetGroup = new ApplicationTargetGroup(this, "silo-atg", {
      vpc,
      targetType: TargetType.IP,
      port: 80,
      protocol: ApplicationProtocol.HTTP,
      healthCheck: {
        interval: Duration.seconds(30),
        path: "/healthz",
        protocol: Protocol.HTTP,
        healthyThresholdCount: 2,
        unhealthyThresholdCount: 3,
      },
      targets: [
        siloService.loadBalancerTarget({
          containerName: siloContainer.containerName,
          containerPort: 80,
        }),
      ],
    });

    const siloListener = loadBalancer.addListener("silo-http-listener", {
      port: 80,
      protocol: ApplicationProtocol.HTTP,
      defaultTargetGroups: [siloTargetGroup],
      open: true,
    });

    siloService.node.addDependency(cluster);
    siloService.node.addDependency(loadBalancer);
  }
}
