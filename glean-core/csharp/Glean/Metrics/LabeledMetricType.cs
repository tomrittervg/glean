﻿// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Mozilla.Glean.FFI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mozilla.Glean.Private
{

    /// <summary>
    /// This empty interface must be implemented by all the submetric types
    /// supported by `LabeledMetricType` in order to allow the type casting
    /// powering its indexer implementation (subscript operator).
    /// </summary>
    public interface ILabeledSubmetricInterface {}

    /// <summary>
    /// This implements the developer facing API for labeled metrics.
    /// 
    /// Instances of this class type are automatically generated by the parsers at build time,
    /// allowing developers to record values that were previously registered in the metrics.yaml file.
    /// 
    /// Unlike most metric types, `LabeledMetricType` does not have its own corresponding storage,
    /// but records metrics for the underlying metric type `T` in the storage for that type.  The
    /// only difference is that labeled metrics are stored with the special key
    /// `$category.$name/$label`. The collect method knows how to pull these special values back
    /// out of the individual metric storage and rearrange them correctly in the ping.
    /// </summary>
    public sealed class LabeledMetricType<T> where T : ILabeledSubmetricInterface
    {
        private bool disabled;
        private string[] sendInPings;
        private T submetric;
        private UInt64 handle;

        /// <exception cref="InvalidOperationException">
        /// If the type of the `submetric` parameter is currently not supported.
        /// </exception>
        public LabeledMetricType(
            bool disabled,
            string category,
            Lifetime lifetime,
            string name,
            string[] sendInPings,
            T submetric,
            HashSet<string> labels = null
            )
        {
            this.disabled = disabled;
            this.sendInPings = sendInPings;
            this.submetric = submetric;

            Func<string, string, string[], int, int, bool, string[], int, UInt64> metricTypeInstantiator;
            switch (submetric)
            {
                case BooleanMetricType _:
                    metricTypeInstantiator = LibGleanFFI.glean_new_labeled_boolean_metric;
                    break;
                case StringMetricType _:
                    metricTypeInstantiator = LibGleanFFI.glean_new_labeled_string_metric;
                    break;
                default:
                    throw new InvalidOperationException("Can not create a labeled version of this metric type");
            }

            handle = metricTypeInstantiator(
                    category,
                    name,
                    sendInPings,
                    sendInPings.Length,
                    (int)lifetime,
                    disabled,
                    labels?.ToArray(),
                    (labels != null) ? labels.Count : 0);
        }

        /// <summary>
        /// Get the specific metric for a given label.
        /// 
        /// If a set of acceptable labels were specified in the metrics.yaml file,
        /// and the given label is not in the set, it will be recorded under the
        /// special `__other__`.
        /// 
        /// If a set of acceptable labels was not specified in the metrics.yaml file,
        /// only the first 16 unique labels will be used. After that, any additional
        /// labels will be recorded under the special `__other__` label.
        /// 
        /// Labels must be snake_case and less than 30 characters. If an invalid label
        /// is used, the metric will be recorded in the special `__other__` label.
        /// </summary>
        /// <param name="label">The label</param>
        /// <exception cref="InvalidOperationException">
        /// If the type of  `T`  is currently not supported.
        /// </exception>
        public T this[string label]
        {
            get {
                // Note the double `(T)(ILabeledSubmetricInterface)` cast before returning. This is
                // required in order to make the compiler not complain. Since the `where` clause for
                // this class cannot list more than one class, we need all our supported subtypes to
                // implement a common interface and use that interface as the T type constraint. This
                // allows us to then explicitly cast bach to T, which is otherwise impossible.
                switch (submetric)
                {
                    case BooleanMetricType _:
                        {
                            UInt64 handle = LibGleanFFI.glean_labeled_boolean_metric_get(this.handle, label);
                            return (T)(ILabeledSubmetricInterface)new BooleanMetricType(handle, disabled, sendInPings);
                        }
                    case StringMetricType _:
                        {
                            UInt64 handle = LibGleanFFI.glean_labeled_string_metric_get(this.handle, label);
                            return (T)(ILabeledSubmetricInterface)new StringMetricType(handle, disabled, sendInPings);
                        }
                    default:
                        throw new InvalidOperationException("Can not get a submetric of this metric type");
                }
            }
        }

        /// <summary>
        /// Returns the number of errors recorded for the given metric.
        /// </summary>
        /// <param name="errorType">The type of the error recorded.</param>
        /// <param name="pingName">Represents the name of the ping to retrieve the metric for.
        /// Defaults to the first value in `sendInPings`.</param>
        /// <returns>the number of errors recorded for the metric.</returns>
        public Int32 TestGetNumRecordedErrors(Testing.ErrorType errorType, string pingName = null)
        {
            Dispatchers.AssertInTestingMode();

            string ping = pingName ?? sendInPings[0];


            switch (submetric)
            {
                case BooleanMetricType _:
                    {
                        return LibGleanFFI.glean_labeled_boolean_test_get_num_recorded_errors(handle, (int)errorType, ping);
                    }
                case StringMetricType _:
                    {
                        return LibGleanFFI.glean_labeled_string_test_get_num_recorded_errors(handle, (int)errorType, ping);
                    }
                default:
                    throw new InvalidOperationException("Can not return errors for this metric type");
            }
        }
    }
}
