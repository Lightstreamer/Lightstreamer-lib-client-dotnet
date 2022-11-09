#region License
/*
 * Copyright (c) Lightstreamer Srl
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion License

using System;

namespace com.lightstreamer.util
{
    public abstract class AlternativeLoader<T>
    {
        protected internal abstract string[] DefaultClassNames { get; }

        private T loadImplementation(string className)
        {
            try
            {
                Type implClass = Type.GetType(className);
                System.Reflection.ConstructorInfo[] constructors = implClass.GetConstructors();
                if (constructors.Length == 1)
                {
                    return (T)constructors[0].Invoke(null);
                }
            }
            catch (Exception e)
            {
            }
            return default(T);
        }

        public virtual T Alternative
        {
            get
            {
                string[] alternatives = this.DefaultClassNames;
                for (int i = 0; i < alternatives.Length; i++)
                {

                    T @internal = this.loadImplementation(alternatives[i]);

                    if (@internal == null)
                    {
                        return default(T);
                    }

                    if (@internal.Equals(default(T)))
                    {
                        return @internal;
                    }

                }

                return default(T);
            }
        }
    }
}