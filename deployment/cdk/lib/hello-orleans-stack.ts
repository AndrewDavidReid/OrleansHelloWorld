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

    const siloTaskDefinition = this.getTaskDefinition("silo-task-def");
    const apiTaskDefinition = this.getTaskDefinition("api-task-def");

    const siloContainer = this.getContainerDefinition(
      siloTaskDefinition,
      "silo-container-def",
      "peiandy/hello-silo:v4",
      [
        {
          containerPort: 80,
        },
        {
          containerPort: 8080,
        },
        {
          containerPort: 11111,
        },
        {
          containerPort: 30000,
        },
      ]
    );

    const apiContainer = this.getContainerDefinition(
      apiTaskDefinition,
      "api-container-def",
      "peiandy/hello-api:v1",
      [
        {
          containerPort: 80,
        },
      ]
    );

    const siloSecurityGroup = new SecurityGroup(this, "silo-sg", {
      vpc,
    });

    const apiSecurityGroup = new SecurityGroup(this, "api-sg", {
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
    // API to Silo
    siloSecurityGroup.addIngressRule(apiSecurityGroup, Port.tcp(30000));
    siloPeeringSecurityGroup.addIngressRule(siloSecurityGroup, Port.tcp(11111));
    siloPeeringSecurityGroup.addEgressRule(siloSecurityGroup, Port.tcp(11111));
    // ALB to API
    apiSecurityGroup.addIngressRule(albSecurityGroup, Port.tcp(80));

    const siloService = new FargateService(this, "silo-fargate-service", {
      cluster,
      desiredCount: 2,
      taskDefinition: siloTaskDefinition,
      healthCheckGracePeriod: Duration.seconds(180),
      securityGroups: [siloSecurityGroup, siloPeeringSecurityGroup],
    });

    const apiService = new FargateService(this, "api-fargate-service", {
      cluster,
      desiredCount: 1,
      taskDefinition: apiTaskDefinition,
      healthCheckGracePeriod: Duration.seconds(180),
      securityGroups: [apiSecurityGroup],
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

    const apiTargetGroup = new ApplicationTargetGroup(this, "api-atg", {
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
        apiService.loadBalancerTarget({
          containerName: apiContainer.containerName,
          containerPort: 80,
        }),
      ],
    });

    const apiListener = loadBalancer.addListener("silo-http-listener", {
      port: 80,
      protocol: ApplicationProtocol.HTTP,
      defaultTargetGroups: [apiTargetGroup],
      open: true,
    });

    // Ensure load balencer is deployed before services
    siloService.node.addDependency(loadBalancer);
    apiService.node.addDependency(loadBalancer);
    // Ensure silo service is deployed before the api service
    apiService.node.addDependency(siloService);
  }

  private getTaskDefinition(taskDefinitionName: string) {
    return new TaskDefinition(this, taskDefinitionName, {
      compatibility: Compatibility.FARGATE,
      networkMode: NetworkMode.AWS_VPC,
      cpu: "512",
      memoryMiB: "1024",
    });
  }

  private getContainerDefinition(
    taskDefinition: TaskDefinition,
    containerName: string,
    imageTag: string,
    portMappings: cdk.aws_ecs.PortMapping[]
  ) {
    return new ContainerDefinition(this, containerName, {
      taskDefinition: taskDefinition,
      environment: {
        ["RUN_ON_AWS_ECS"]: "true",
        ["MONGO_CONNECTION_STRING"]: process.env.MONGO_CONNECTION_STRING ?? "",
      },
      healthCheck: {
        command: ["CMD-SHELL", "curl -f http://localhost/healthz || exit 1"],
      },
      image: ContainerImage.fromRegistry(imageTag),
      essential: true,
      logging: LogDriver.awsLogs({
        streamPrefix: containerName,
        logRetention: RetentionDays.ONE_DAY,
        mode: AwsLogDriverMode.NON_BLOCKING,
      }),
      portMappings,
    });
  }
}
