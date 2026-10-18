using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

int passed = 0;
int failed = 0;
foreach (Type fixtureType in Assembly.GetExecutingAssembly().GetTypes().OrderBy(type => type.Name))
{
    MethodInfo[] tests = fixtureType.GetMethods().Where(method => method.IsDefined(typeof(TestAttribute)) || method.IsDefined(typeof(TestCaseAttribute))).ToArray();
    if (tests.Length == 0) continue;
    MethodInfo setup = fixtureType.GetMethods().SingleOrDefault(method => method.IsDefined(typeof(SetUpAttribute)));
    MethodInfo teardown = fixtureType.GetMethods().SingleOrDefault(method => method.IsDefined(typeof(TearDownAttribute)));
    foreach (MethodInfo test in tests)
    {
        object[][] cases = test.GetCustomAttributes<TestCaseAttribute>().Select(testCase => testCase.Arguments).ToArray();
        if (cases.Length == 0) cases = new[] { Array.Empty<object>() };
        foreach (object[] arguments in cases)
        {
            object fixture = Activator.CreateInstance(fixtureType);
            string name = fixtureType.Name + "." + test.Name + "(" + string.Join(", ", arguments) + ")";
            Exception failure = null;
            try
            {
                setup?.Invoke(fixture, null);
                test.Invoke(fixture, arguments);
            }
            catch (Exception error)
            {
                failure = error is TargetInvocationException ? error.InnerException : error;
            }
            finally
            {
                try { teardown?.Invoke(fixture, null); }
                catch (Exception error) { failure ??= error is TargetInvocationException ? error.InnerException : error; }
            }
            if (failure == null)
            {
                Console.WriteLine("PASS " + name);
                passed++;
            }
            else
            {
                Console.WriteLine("FAIL " + name + ": " + failure);
                failed++;
            }
        }
    }
}
Console.WriteLine($"{passed} passed; {failed} failed. Source simulation only: no Unity, Udon VM, or VRChat transport.");
return failed == 0 && passed > 0 ? 0 : 1;
