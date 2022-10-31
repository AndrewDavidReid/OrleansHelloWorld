#!/usr/bin/env node
import "source-map-support/register";
import * as cdk from "aws-cdk-lib";
import { HelloOrleansStack } from "../lib/hello-orleans-stack";

const app = new cdk.App();
new HelloOrleansStack(app, "HelloOrleansStack");
