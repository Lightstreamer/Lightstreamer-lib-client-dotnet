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