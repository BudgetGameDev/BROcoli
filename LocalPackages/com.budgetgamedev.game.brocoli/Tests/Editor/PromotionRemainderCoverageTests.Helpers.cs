using System;
using System.Reflection;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class PromotionRemainderCoverageTests
    {
        private static object Invoke(object target, string name, params object[] arguments)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
                foreach (MethodInfo method in type.GetMethods(Hidden))
                    if (method.Name == name && method.GetParameters().Length == arguments.Length)
                        return method.Invoke(target, arguments);
            throw new MissingMethodException(target.GetType().Name, name);
        }

        private static string DescribeCaptures()
        {
            var host = new GameObject("Promotion capture summary");
            host.SetActive(false);
            try
            {
                var telemetry = host.AddComponent<RunTelemetry>();
                telemetry.Configure(new AutoplayConfig { CaptureEnabled = true });
                return (string)Invoke(telemetry, "DescribeCaptures");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void Set(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, Hidden);
                if (field == null)
                    continue;
                field.SetValue(target, value);
                return;
            }
            throw new MissingFieldException(target.GetType().Name, name);
        }

        private static void SetStatic(Type type, string name, object value) =>
            type.GetField(name, Hidden).SetValue(null, value);
    }
}
