using System;
using System.Linq;
using NUnit.Framework;

namespace Mejnun.Core.Tests
{
    public sealed class CoreAssemblyTests
    {
        [Test]
        public void CoreAssembly_DoesNotReferenceUnityEngine()
        {
            var coreAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(assembly => assembly.GetName().Name == "Mejnun.Core");

            Assert.That(coreAssembly, Is.Not.Null, "Mejnun.Core must be loaded by the test assembly.");

            var unityReference = coreAssembly.GetReferencedAssemblies()
                .FirstOrDefault(reference => reference.Name == "UnityEngine" ||
                                             reference.Name.StartsWith("UnityEngine.", StringComparison.Ordinal));

            Assert.That(unityReference, Is.Null, $"Mejnun.Core references {unityReference?.Name}.");
        }
    }
}