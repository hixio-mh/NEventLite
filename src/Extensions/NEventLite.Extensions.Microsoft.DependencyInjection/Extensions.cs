﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NEventLite.Repository;
using NEventLite.Util;

namespace NEventLite.Extensions.Microsoft.DependencyInjection
{
    public static class Extensions
    {
        public static void ScanAndRegisterAggregates(this ServiceCollection services)
        {
            services.ScanAndRegisterAggregates(AppDomain.CurrentDomain.GetAssemblies());
        }

        public static void ScanAndRegisterAggregates(this ServiceCollection services, IList<Assembly> assemblies)
        {
            foreach (var a in assemblies.GetAllAggregates())
            {
                services.RegisterAggregate(a);
            }
        }

        public static void RegisterAggregate(this ServiceCollection services, AggregateInformation a)
        {
            // Register full generic types
            services.AddScoped(typeof(IRepository<,,>).MakeGenericType(a.Aggregate, a.AggregateKey, a.EventKey),
                a.Snapshot != null
                    ? typeof(Repository<,,,,>).MakeGenericType(a.Aggregate, a.Snapshot, a.AggregateKey,
                        a.EventKey, a.SnapshotKey)
                    : typeof(EventOnlyRepository<,,>).MakeGenericType(a.Aggregate, a.AggregateKey, a.EventKey));

            services.AddScoped(typeof(ISession<,,>).MakeGenericType(a.Aggregate, a.AggregateKey, a.EventKey),
                typeof(Session<,,>).MakeGenericType(a.Aggregate, a.AggregateKey, a.EventKey));

            // Register the convenience GUID scoped ISession interface as well
            if (a.AggregateKey == typeof(Guid) && a.EventKey == typeof(Guid))
            {
                services.AddScoped(typeof(ISession<>).MakeGenericType(a.Aggregate),
                    typeof(Session<>).MakeGenericType(a.Aggregate));
            }
        }
    }
}
