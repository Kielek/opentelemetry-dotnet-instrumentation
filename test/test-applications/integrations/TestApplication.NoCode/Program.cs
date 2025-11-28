// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using TestApplication.Shared;

ConsoleHelper.WriteSplashScreen(args);

var testClass = new ClassLibrary1.Net8.Class1();
testClass.Do();
