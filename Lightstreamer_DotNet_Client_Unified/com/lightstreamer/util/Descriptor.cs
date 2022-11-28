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
    public abstract class Descriptor : ICloneable
    {
        protected internal Descriptor subDescriptor = null;
        private int length = 0;

        public virtual Descriptor SubDescriptor
        {
            set
            {
                this.subDescriptor = value;
            }
            get
            {
                return this.subDescriptor;
            }
        }

        public virtual int Size
        {
            get
            {
                return this.length;
            }
            set
            {
                this.length = value;
            }
        }

        public virtual int FullSize
        {
            get
            {
                if (this.subDescriptor != null)
                {
                    return this.Size + this.subDescriptor.Size;
                }
                return this.Size;
            }
        }

        public abstract int getPos(string name);
        public abstract string getName(int pos);
        public abstract string ComposedString { get; }

        public Object Clone()
        {
            try
            {
                Descriptor copy = (Descriptor)base.MemberwiseClone();
                if (this.subDescriptor != null)
                {
                    copy.subDescriptor = (Descriptor)subDescriptor.Clone();
                }
                return copy;
            }
            catch (Exception)
            {
                throw new Exception(); // should not happen
            }
        }
    }
}