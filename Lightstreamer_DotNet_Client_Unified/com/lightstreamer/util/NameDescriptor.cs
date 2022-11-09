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
    public class NameDescriptor : Descriptor
    {
        private string name;

        public NameDescriptor(string name)
        {
            this.name = name;
        }

        public override int getPos(string name)
        {
            if (this.subDescriptor != null)
            {
                int fromSub = this.subDescriptor.getPos(name);
                return fromSub > -1 ? fromSub + this.Size : -1;
            }
            return -1;
        }

        public override string getName(int pos)
        {
            if (this.subDescriptor != null)
            {
                return this.subDescriptor.getName(pos - this.Size);
            }
            return null;
        }

        public override string ComposedString
        {
            get
            {
                return this.name;
            }
        }

        public virtual string Original
        {
            get
            {
                return this.name;
            }
        }

        public Object Clone()
        {
            return (NameDescriptor)base.MemberwiseClone();
        }
    }
}