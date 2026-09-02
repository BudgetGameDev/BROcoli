using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed class LicensedAssetDecryptorRetryTests
    {
        private const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void RetryFileSystemOperationRetriesTransientFailuresWithExponentialBackoff()
        {
            int attempts = 0;
            var delays = new List<int>();

            InvokeRetry(
                () =>
                {
                    attempts++;
                    if (attempts < 3)
                        throw new IOException("Simulated transient file lock.");
                },
                delays.Add,
                6
            );

            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(delays, Is.EqualTo(new[] { 50, 100 }));
        }

        [Test]
        public void RetryFileSystemOperationDoesNotRetryNonFileSystemFailures()
        {
            int attempts = 0;
            var delays = new List<int>();

            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
                InvokeRetry(
                    () =>
                    {
                        attempts++;
                        throw new InvalidOperationException("Simulated programming error.");
                    },
                    delays.Add,
                    6
                )
            );

            Assert.That(invocation.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(delays, Is.Empty);
        }

        [Test]
        public void RetryFileSystemOperationDoesNotRetryDeterministicIoFailures()
        {
            int attempts = 0;
            var delays = new List<int>();

            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
                InvokeRetry(
                    () =>
                    {
                        attempts++;
                        throw new DirectoryNotFoundException("Simulated missing source path.");
                    },
                    delays.Add,
                    6
                )
            );

            Assert.That(invocation.InnerException, Is.TypeOf<DirectoryNotFoundException>());
            Assert.That(attempts, Is.EqualTo(1));
            Assert.That(delays, Is.Empty);
        }

        [Test]
        public void RetryFileSystemOperationRethrowsAfterMaximumAttemptsWithDiagnostics()
        {
            int attempts = 0;
            var delays = new List<int>();

            TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
                InvokeRetry(
                    () =>
                    {
                        attempts++;
                        throw new UnauthorizedAccessException("Simulated persistent file lock.");
                    },
                    delays.Add,
                    3
                )
            );

            Exception failure = invocation.InnerException;
            Assert.That(failure, Is.TypeOf<UnauthorizedAccessException>());
            Assert.That(failure.Data["LicensedAssetOperation"], Is.EqualTo("test operation"));
            Assert.That(failure.Data["LicensedAssetAttempts"], Is.EqualTo(3));
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(delays, Is.EqualTo(new[] { 50, 100 }));
        }

        private static void InvokeRetry(Action operation, Action<int> delay, int maximumAttempts)
        {
            RetryMethod()
                .Invoke(null, new object[] { operation, "test operation", delay, maximumAttempts });
        }

        private static MethodInfo RetryMethod()
        {
            Type decryptorType = AppDomain
                .CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("LicensedAssetDecryptor"))
                .Single(type => type != null);
            MethodInfo retry = decryptorType.GetMethod(
                "RetryFileSystemOperation",
                HiddenStatic,
                null,
                new[] { typeof(Action), typeof(string), typeof(Action<int>), typeof(int) },
                null
            );
            Assert.That(retry, Is.Not.Null);
            return retry;
        }
    }
}
