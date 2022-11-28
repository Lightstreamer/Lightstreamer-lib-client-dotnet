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
using System.Collections.Generic;
using System.Text;

namespace com.lightstreamer.util
{
    public class ListDescriptor : Descriptor
    {
        private string[] list;
        private IDictionary<string, int> reverseList;

        public ListDescriptor(string[] list)
        {
            this.list = list;
            this.reverseList = getReverseList(list);
            base.Size = list.Length;
        }

        private static IDictionary<string, int> getReverseList(string[] list)
        {
            IDictionary<string, int> reverseList = new Dictionary<string, int>();
            for (int i = 0; i < list.Length; i++)
            {
                reverseList[list[i]] = i + 1;
            }
            return reverseList;
        }


        public override int Size
        {
            set
            {
                //can't manually set the size in the list case
            }
        }


        public override string ComposedString
        {
            get
            {
                if (list.Length <= 0)
                {
                    return "";
                }

                StringBuilder joined = new StringBuilder(list[0]);
                for (int i = 1; i < list.Length; i++)
                {
                    joined.Append(" ").Append(list[i]);
                }

                return joined.ToString();

            }
        }

        public override int getPos(string name)
        {
            if (this.reverseList.ContainsKey(name))
            {
                return this.reverseList[name];
            }
            else if (this.subDescriptor != null)
            {
                int fromSub = this.subDescriptor.getPos(name);
                return fromSub > -1 ? fromSub + this.Size : -1;
            }
            return -1;
        }

        public override string getName(int pos)
        {
            if (pos > this.Size)
            {
                if (this.subDescriptor != null)
                {
                    return this.subDescriptor.getName(pos - this.Size);
                }
            }
            else if (pos >= 1)
            {
                return this.list[pos - 1];
            }

            return null;
        }

        public virtual string[] Original
        {
            get
            {
                return this.list;
            }
        }

        private const string NO_EMPTY = " name cannot be empty";
        private const string NO_SPACE = " name cannot contain spaces";
        private const string NO_NUMBER = " name cannot be a number";

        public static void checkItemNames(string[] names, string head)
        {
            if (names == null)
            {
                return;
            }
            for (int i = 0; i < names.Length; i++)
            {
                if (string.ReferenceEquals(names[i], null) || names[i].Equals(""))
                {
                    throw new System.ArgumentException(head + NO_EMPTY);

                }
                else if (names[i].IndexOf(" ", StringComparison.Ordinal) > -1)
                {
                    // An item name cannot contain spaces
                    throw new System.ArgumentException(head + NO_SPACE);

                }
                else if (Number.isNumber(names[i]))
                {
                    // An item name cannot be a number
                    throw new System.ArgumentException(head + NO_NUMBER);
                }
            }
        }

        public static void checkFieldNames(string[] names, string head)
        {
            if (names == null)
            {
                return;
            }
            for (int i = 0; i < names.Length; i++)
            {
                if (string.ReferenceEquals(names[i], null) || names[i].Equals(""))
                {
                    throw new System.ArgumentException(head + NO_EMPTY);

                }
                else if (names[i].IndexOf(" ", StringComparison.Ordinal) > -1)
                {
                    // A field name cannot contain spaces
                    throw new System.ArgumentException(head + NO_SPACE);
                }
            }
        }

        public ListDescriptor MemberwiseClone()
        {
            ListDescriptor copy = (ListDescriptor)base.MemberwiseClone();
            copy.list = (string[])list.Clone();
            copy.reverseList = new Dictionary<string, int>(reverseList);
            return copy;
        }
    }
}