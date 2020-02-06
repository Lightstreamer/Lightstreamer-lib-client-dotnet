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