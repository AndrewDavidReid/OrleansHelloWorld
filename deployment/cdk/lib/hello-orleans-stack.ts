import * as cdk from "aws-cdk-lib";
import { Construct } from "constructs";
import { SecurityGroup, Vpc } from "aws-cdk-lib/aws-ec2";
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
  CfnListenerRule,
  Protocol,
  TargetType,
} from "aws-cdk-lib/aws-elasticloadbalancingv2";

export class HelloOrleansStack extends cdk.Stack {
  constructor(scope: Construct, id: string, props?: cdk.StackProps) {
    super(scope, id, props);
    // Container registry

    // example resource
    const vpc = new Vpc(this, "OrleansVpc", {});

    const cluster = new Cluster(this, "MyCluster", {
      vpc: vpc,
    });

    // Task execution Role

    const siloTaskDefinition = new TaskDefinition(this, "SiloTask", {
      compatibility: Compatibility.FARGATE,
      // task role
      networkMode: NetworkMode.AWS_VPC,
      cpu: "512",
      memoryMiB: "1024",
    });

    // HealthCheck
    const healthCheck: HealthCheck = {
      // TODO: Check port
      command: ["CMD-SHELL", "curl -f http://localhost/health || exit 1"],
    };

    const siloContainer = new ContainerDefinition(this, "SiloContainer", {
      taskDefinition: siloTaskDefinition,
      // TODO: Image here.
      environment: {
        // key value pairs
      },
      healthCheck,
      cpu: 1,
      memoryReservationMiB: 512,
      image: ContainerImage.fromRegistry("peiandy/hello-silo:v1"),
      essential: true,
      portMappings: [
        {
          containerPort: 11111,
        },
        {
          containerPort: 30000,
        },
      ],
    });
    // todo: name
    const securityGroup = new SecurityGroup(this, "name", {
      vpc,
      description: "",
    });

    // silo sg
    // alb sg
    // api sg
    // extra silo sg
    // securityGroup.addIngressRule();
    // securityGroup.addEgressRule();

    // todo; alb security group

    const siloService = new FargateService(this, "SiloFargateService", {
      cluster,
      desiredCount: 2,
      taskDefinition: siloTaskDefinition,
      healthCheckGracePeriod: Duration.seconds(180),
      securityGroups: [],
    });

    const loadBalancer = new ApplicationLoadBalancer(this, "alb", {
      vpc,
      securityGroup,
      idleTimeout: Duration.seconds(180),
      // TODO: Read up on this.
      internetFacing: true,
    });

    const targetGroup = new ApplicationTargetGroup(this, "atg", {
      targetType: TargetType.IP,
      port: 8080,
      protocol: ApplicationProtocol.HTTP,
      vpc,
      healthCheck: {
        interval: Duration.seconds(180),
        // todo: health check endpoin
        path: "",
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
    });

    const listenerHttps = loadBalancer.addListener("lisHttpsApi", {
      port: 443,
      protocol: ApplicationProtocol.HTTPS,
      // TODO:
      certificates: [],
      defaultTargetGroups: [targetGroup],
      open: false,
    });

    const listenerRuleHttps = new CfnListenerRule(this, "lrHttps", {
      listenerArn: listenerHttps.listenerArn,
      priority: 1,
      actions: [
        {
          type: "forward",
          targetGroupArn: targetGroup.targetGroupArn,
        },
      ],
      conditions: [
        {
          field: "path-pattern",
          values: ["*"],
        },
      ],
    });

    // cname?
    siloService.node.addDependency(cluster);
    siloService.node.addDependency(loadBalancer);
  }
}
