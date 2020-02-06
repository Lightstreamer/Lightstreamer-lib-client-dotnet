using System;

/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 *
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
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