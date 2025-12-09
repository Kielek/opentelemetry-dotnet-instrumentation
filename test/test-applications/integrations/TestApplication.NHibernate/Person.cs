// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;

namespace TestApplication.NHibernate
{
    public class Person
    {
        public virtual int Id { get; set; }

        public virtual string Name { get; set; } = string.Empty;

        public virtual int Age { get; set; }
    }
}
