﻿// Copyright (C) 2011, Mehdi Khalili
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the <organization> nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using TestStack.BDDfy.Core;

namespace TestStack.BDDfy.Scanners.StepScanners.MethodName
{
    /// <summary>
    /// Uses reflection to scan a scenario class for steps using method name conventions
    /// </summary>
    /// <remarks>
    /// Method names starting with the following words are considered as steps and are
    /// reported: 
    /// <list type="bullet">
    /// <item>
    /// <description><i>Given: </i>setup step </description></item>
    /// <item>
    /// <description><i>AndGiven: </i>setup step running after 'Given' steps
    /// </description></item>
    /// <item>
    /// <description><i>When: </i>state transition step </description></item>
    /// <item>
    /// <description><i>AndWhen: </i>state transition step running after 'When' steps
    /// </description></item>
    /// <item>
    /// <description><i>Then: </i>asserting step </description></item>
    /// <item>
    /// <description><i>And: </i>asserting step running after 'Then' steps
    /// </description></item></list>
    /// <para>A method ending with <i>Context </i>is considered as a setup method (not
    /// reported). </para>
    /// <para>A method starting with <i>Setup </i>is considered as a setup method (not
    /// reported). </para>
    /// <para>A method starting with <i>TearDown </i>is considered as a finally method
    /// which is run after all the other steps (not reported). </para>
    /// </remarks>
    public class MethodNameStepScanner : IStepScanner
    {
        private readonly Func<string, string> _stepTextTransformer;
        private readonly MethodNameMatcher[] _matchers;

        public MethodNameStepScanner(Func<string, string> stepTextTransformer, params MethodNameMatcher[] matchers)
        {
            _stepTextTransformer = stepTextTransformer;
            _matchers = matchers;
        }

        public IEnumerable<ExecutionStep> Scan(object testObject, MethodInfo method)
        {
            foreach (var matcher in _matchers)
            {
                if (!matcher.IsMethodOfInterest(method.Name)) 
                    continue;

                var argAttributes = (RunStepWithArgsAttribute[])method.GetCustomAttributes(typeof(RunStepWithArgsAttribute), false);
                var returnsItsText = method.ReturnType == typeof(IEnumerable<string>);

                if (argAttributes.Length == 0)
                    yield return GetStep(testObject, matcher, method, returnsItsText);

                foreach (var argAttribute in argAttributes)
                {
                    var inputs = argAttribute.InputArguments;
                    if (inputs != null && inputs.Length > 0)
                        yield return GetStep(testObject, matcher, method, returnsItsText, inputs, argAttribute);
                }

                yield break;
            }
        }

        private ExecutionStep GetStep(object testObject, MethodNameMatcher matcher, MethodInfo method, bool returnsItsText, object[] inputs = null, RunStepWithArgsAttribute argAttribute = null)
        {
            var stepMethodName = GetStepTitle(method, testObject, argAttribute, returnsItsText);
            var stepAction = GetStepAction(method, inputs, returnsItsText);
            return new ExecutionStep(stepAction, stepMethodName, matcher.Asserts, matcher.ExecutionOrder, matcher.ShouldReport);
        }

        private string GetStepTitle(MethodInfo method, object testObject, RunStepWithArgsAttribute argAttribute, bool returnsItsText)
        {
            Func<string> stepTitleFromMethodName = () => GetStepTitleFromMethodName(method, argAttribute);

            if(returnsItsText)
                return GetStepTitleFromMethod(method, argAttribute, testObject) ?? stepTitleFromMethodName();

            return stepTitleFromMethodName();
        }

        private string GetStepTitleFromMethodName(MethodInfo method, RunStepWithArgsAttribute argAttribute)
        {
            var methodName = _stepTextTransformer(NetToString.Convert(method.Name));
            object[] inputs = null;

            if (argAttribute != null && argAttribute.InputArguments != null)
                inputs = argAttribute.InputArguments;

            if (inputs == null)
                return methodName;
            
            if (string.IsNullOrEmpty(argAttribute.StepTextTemplate))
            {
                var stringFlatInputs = inputs.FlattenArrays().Select(i => i.ToString()).ToArray();
                return methodName + " " + string.Join(", ", stringFlatInputs);
            }

            return string.Format(argAttribute.StepTextTemplate, inputs.FlattenArrays());
        }

        private static string GetStepTitleFromMethod(MethodInfo method, RunStepWithArgsAttribute argAttribute, object testObject)
        {
            object[] inputs = null;
            if(argAttribute != null && argAttribute.InputArguments != null)
                inputs = argAttribute.InputArguments;

            var enumerableResult = InvokeIEnumerableMethod(method, testObject, inputs);
            try
            {
                return enumerableResult.FirstOrDefault();
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    "The signature of method '{0}' indicates that it returns its step title; but the code is throwing an exception before a title is returned",
                    method.Name);
                throw new StepTitleException(message, ex);
            }
        }

        static Action<object> GetStepAction(MethodInfo method, object[] inputs, bool returnsItsText)
        {
            if (returnsItsText)
            {
                // Note: Count() is a silly trick to enumerate over the method and make sure it returns because it is an IEnumerable method and runs lazily otherwise
                return o => InvokeIEnumerableMethod(method, o, inputs).Count();
            }

            return o => method.Invoke(o, inputs);
        }

        private static IEnumerable<string> InvokeIEnumerableMethod(MethodInfo method, object testObject, object[] inputs)
        {
            return (IEnumerable<string>)method.Invoke(testObject, inputs);
        }
    }
}
