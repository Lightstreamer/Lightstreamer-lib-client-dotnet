using System.Collections.Generic;
using System.Collections.Immutable;

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
namespace com.lightstreamer.client
{
    using Descriptor = com.lightstreamer.util.Descriptor;
    using NameDescriptor = com.lightstreamer.util.NameDescriptor;

    /// <summary>
    /// Contains all the information related to an update of the field values for an item. 
    /// It reports all the new values of the fields.<br/>
    /// <br/>
    /// <b>COMMAND Subscription</b><br/>
    /// If the involved Subscription is a COMMAND Subscription, then the values for the current 
    /// update are meant as relative to the same key.<br/>
    /// Moreover, if the involved Subscription has a two-level behavior enabled, then each update 
    /// may be associated with either a first-level or a second-level item. In this case, the reported 
    /// fields are always the union of the first-level and second-level fields and each single update 
    /// can only change either the first-level or the second-level fields (but for the "command" field, 
    /// which is first-level and is always set to "UPDATE" upon a second-level update); note 
    /// that the second-level field values are always null until the first second-level update 
    /// occurs). When the two-level behavior is enabled, in all methods where a field name has to 
    /// be supplied, the following convention should be followed:<br/>
    /// <br/>
    /// <ul>
    ///  <li>The field name can always be used, both for the first-level and the second-level fields. 
    ///  In case of name conflict, the first-level field is meant.</li>
    ///  <li>The field position can always be used; however, the field positions for the second-level 
    ///  fields start at the highest position of the first-level field list + 1. If a field schema had 
    ///  been specified for either first-level or second-level Subscriptions, then client-side knowledge 
    ///  of the first-level schema length would be required.</li>
    /// </ul>
    /// </summary>
    public class ItemUpdate
    {
        private readonly string itemName;
        private readonly int itemPos;
        private readonly bool isSnapshot;
        private readonly Descriptor fields;
        private readonly List<string> updates;
        private readonly ISet<int> changedFields;

        private IDictionary<string, string> changedByNameMap;
        private IDictionary<int, string> changedByPosMap;
        private IDictionary<string, string> allByNameMap;
        private IDictionary<int, string> allByPosMap;

        internal ItemUpdate(string itemName, int itemPos, bool isSnapshot, List<string> updates, SortedSet<int> changedFields, Descriptor fields)
        {
            this.itemName = itemName;
            this.itemPos = itemPos;
            this.isSnapshot = isSnapshot;
            this.updates = updates;
            this.changedFields = changedFields;
            this.fields = fields;
        }

        /// <value>
        /// Read-only property <c>ItemName</c> represents the name of the item to which this update pertains.<br/> 
        /// The name will be null if the related Subscription was initialized using an "Item Group".<br/>
        /// See also: <seealso cref="Subscription.ItemGroup"/>, <seealso cref="Subscription.Items"/>.<br/>
        /// </value>
        public virtual string ItemName
        {
            get
            {
                return this.itemName;
            }
        }

        /// <value>
        /// Read-only property <c>ItemPos</c> represents the 1-based the position in the "Item List" or
        /// "Item Group" of the item to which this update pertains.<br/>
        /// See also: <seealso cref="Subscription.ItemGroup"/>, <seealso cref="Subscription.Items"/>.<br/>
        /// </value>
        public virtual int ItemPos
        {
            get
            {
                return this.itemPos;
            }
        }

        /// <summary>
        /// Returns the current value for the specified field 
        /// </summary>
        /// <param name="fieldName"> The field name as specified within the "Field List". </param>
        /// <returns> The value of the specified field; it can be null in the following cases:<br/>
        /// <ul>
        ///  <li>a null value has been received from the Server, as null is a possible value for a field;</li>
        ///  <li>no value has been received for the field yet;</li>
        ///  <li>the item is subscribed to with the COMMAND mode and a DELETE command is received 
        ///  (only the fields used to carry key and command informations are valued).</li>
        /// </ul> 
        /// </returns>
        /// <seealso cref="Subscription.Fields" />
        public virtual string getValue(string fieldName)
        {
            int pos = toPos(fieldName);
            return this.updates[pos - 1]; //fieldPos is 1 based, updates is 0 based
        }

        /// <summary>
        /// Returns the current value for the specified field </summary>
        /// <param name="fieldPos"> The 1-based position of the field within the "Field List" or
        /// "Field Schema".</param>
        /// <returns> The value of the specified field; it can be null in the following cases:<br/>
        /// <ul>
        ///  <li>a null value has been received from the Server, as null is a possible value for a field;</li>
        ///  <li>no value has been received for the field yet;</li>
        ///  <li>the item is subscribed to with the COMMAND mode and a DELETE command is received 
        ///  (only the fields used to carry key and command informations are valued).</li>
        /// </ul> </returns>
        /// <seealso cref="Subscription.FieldSchema" />
        /// <seealso cref="Subscription.Fields" />
        public virtual string getValue(int fieldPos)
        {
            int pos = toPos(fieldPos);
            return this.updates[pos - 1]; //fieldPos is 1 based, updates is 0 based
        }

        /// <summary>
        /// Inquiry method that asks whether the current update belongs to the item snapshot (which carries 
        /// the current item state at the time of Subscription). Snapshot events are sent only if snapshot 
        /// information was requested for the items through <seealso cref="Subscription.RequestedSnapshot"/> 
        /// and precede the real time events. Snapshot informations take different forms in different 
        /// subscription modes and can be spanned across zero, one or several update events. In particular:
        /// <ul>
        ///  <li>if the item is subscribed to with the RAW subscription mode, then no snapshot is 
        ///  sent by the Server;</li>
        ///  <li>if the item is subscribed to with the MERGE subscription mode, then the snapshot consists 
        ///  of exactly one event, carrying the current value for all fields;</li>
        ///  <li>if the item is subscribed to with the DISTINCT subscription mode, then the snapshot 
        ///  consists of some of the most recent updates; these updates are as many as specified 
        ///  through <seealso cref="Subscription.RequestedSnapshot"/>, unless fewer are available;</li>
        ///  <li>if the item is subscribed to with the COMMAND subscription mode, then the snapshot 
        ///  consists of an "ADD" event for each key that is currently present.</li>
        /// </ul>
        /// Note that, in case of two-level behavior, snapshot-related updates for both the first-level item
        /// (which is in COMMAND mode) and any second-level items (which are in MERGE mode) are qualified with 
        /// this flag. </summary>
        /// <returns> true if the current update event belongs to the item snapshot; false otherwise. </returns>
        public virtual bool Snapshot
        {
            get
            {
                return this.isSnapshot;
            }
        }

        /// <summary>
        /// Inquiry method that asks whether the value for a field has changed after the reception of the last 
        /// update from the Server for an item. If the Subscription mode is COMMAND then the change is meant as 
        /// relative to the same key. </summary>
        /// <param name="fieldName"> The field name as specified within the "Field List". </param>
        /// <returns> Unless the Subscription mode is COMMAND, the return value is true in the following cases:
        /// <ul>
        ///  <li>It is the first update for the item;</li>
        ///  <li>the new field value is different than the previous field 
        ///  value received for the item.</li>
        /// </ul>
        ///  If the Subscription mode is COMMAND, the return value is true in the following cases:
        /// <ul>
        ///  <li>it is the first update for the involved key value (i.e. the event carries an "ADD" command);</li>
        ///  <li>the new field value is different than the previous field value received for the item, 
        ///  relative to the same key value (the event must carry an "UPDATE" command);</li>
        ///  <li>the event carries a "DELETE" command (this applies to all fields other than the field 
        ///  used to carry key information).</li>
        /// </ul>
        /// In all other cases, the return value is false. </returns>
        /// <seealso cref="Subscription.Fields" />
        public virtual bool isValueChanged(string fieldName)
        {
            int pos = toPos(fieldName);
            return this.changedFields.Contains(pos);
        }

        /// <summary>
        /// Inquiry method that asks whether the value for a field has changed after the reception of the last 
        /// update from the Server for an item. If the Subscription mode is COMMAND then the change is meant as 
        /// relative to the same key. </summary>
        /// <param name="fieldPos"> The 1-based position of the field within the "Field List" or "Field Schema". </param>
        /// <returns> Unless the Subscription mode is COMMAND, the return value is true in the following cases:
        /// <ul>
        ///  <li>It is the first update for the item;</li>
        ///  <li>the new field value is different than the previous field 
        ///  value received for the item.</li>
        /// </ul>
        ///  If the Subscription mode is COMMAND, the return value is true in the following cases:
        /// <ul>
        ///  <li>it is the first update for the involved key value (i.e. the event carries an "ADD" command);</li>
        ///  <li>the new field value is different than the previous field value received for the item, 
        ///  relative to the same key value (the event must carry an "UPDATE" command);</li>
        ///  <li>the event carries a "DELETE" command (this applies to all fields other than the field 
        ///  used to carry key information).</li>
        /// </ul>
        /// In all other cases, the return value is false. </returns>
        /// <seealso cref="Subscription.FieldSchema" />
        /// <seealso cref="Subscription.Fields" />
        public virtual bool isValueChanged(int fieldPos)
        {
            int pos = toPos(fieldPos);
            return this.changedFields.Contains(pos);
        }

        /// <summary>
        /// Returns an immutable Map containing the values for each field changed with the last server update. 
        /// The related field name is used as key for the values in the map. 
        /// Note that if the Subscription mode of the involved Subscription is COMMAND, then changed fields 
        /// are meant as relative to the previous update for the same key. On such tables if a DELETE command 
        /// is received, all the fields, excluding the key field, will be present as changed, with null value. 
        /// All of this is also true on tables that have the two-level behavior enabled, but in case of 
        /// DELETE commands second-level fields will not be iterated.
        /// </summary>
        /// <returns> An immutable Map containing the values for each field changed with the last
        /// server update.
        /// </returns>
        /// <seealso cref="Subscription.FieldSchema" />
        /// <seealso cref="Subscription.Fields" />
        public virtual IDictionary<string, string> ChangedFields
        {
            get
            {
                if (fields is NameDescriptor)
                {
                    throw new System.InvalidOperationException("This Subscription was initiated using a field schema, use getChangedFieldsByPosition instead of using getChangedFields");
                }

                return changedByName();
            }
        }

        /// <summary>
        /// Returns an immutable Map containing the values for each field changed with the last server update. 
        /// The 1-based field position within the field schema or field list is used as key for the values in the map. 
        /// Note that if the Subscription mode of the involved Subscription is COMMAND, then changed fields 
        /// are meant as relative to the previous update for the same key. On such tables if a DELETE command 
        /// is received, all the fields, excluding the key field, will be present as changed, with null value. 
        /// All of this is also true on tables that have the two-level behavior enabled, but in case of 
        /// DELETE commands second-level fields will not be iterated.
        /// </summary>
        /// <returns> An immutable Map containing the values for each field changed with the last server update.
        /// </returns>
        /// <seealso cref="Subscription.FieldSchema" />
        /// <seealso cref="Subscription.Fields" />
        public virtual IDictionary<int, string> ChangedFieldsByPosition
        {
            get
            {
                return changedByPos();
            }
        }

        /// <summary>
        /// Returns an immutable Map containing the values for each field in the Subscription.
        /// The related field name is used as key for the values in the map. 
        /// </summary>
        /// <returns> An immutable Map containing the values for each field in the Subscription.
        /// </returns>
        /// <seealso cref="Subscription.FieldSchema" />
        /// <seealso cref="Subscription.Fields" />
        public virtual IDictionary<string, string> Fields
        {
            get
            {
                if (fields is NameDescriptor)
                {
                    throw new System.InvalidOperationException("This Subscription was initiated using a field schema, use getFieldsByPosition instead of using getFields");
                }

                return allByName();
            }
        }

        /// <summary>
        /// Returns an immutable Map containing the values for each field in the Subscription.
        /// The 1-based field position within the field schema or field list is used as key for the values in the map. 
        /// </summary>
        /// <returns> An immutable Map containing the values for each field in the Subscription.
        /// </returns>
        /// <seealso cref="Subscription.FieldSchema" />
        /// <seealso cref="Subscription.Fields" />
        public virtual IDictionary<int, string> FieldsByPosition
        {
            get
            {
                return allByPos();
            }
        }

        internal virtual int FieldsCount
        {
            get
            {
                return this.fields.Size;
            }
        }

        private int toPos(string fieldName)
        {
            int fieldPos = fields.getPos(fieldName);
            if (fieldPos == -1)
            {
                throw new System.ArgumentException("the specified field does not exist");
            }
            return fieldPos;
        }

        private int toPos(int fieldPos)
        {
            if (fieldPos < 1 || fieldPos > this.updates.Count)
            {
                throw new System.ArgumentException("the specified field position is out of bounds");
            }

            return fieldPos;
        }

        private IDictionary<string, string> changedByName()
        {
            if (this.changedByNameMap == null)
            {
                SortedDictionary<string, string> res = new SortedDictionary<string, string>(new OrderedFieldNamesComparator(this));
                foreach (int pos in changedFields)
                {
                    res[fields.getName(pos)] = updates[pos - 1];
                }

                changedByNameMap = ImmutableSortedDictionary.CreateRange(res);
            }

            return changedByNameMap;
        }

        private IDictionary<int, string> changedByPos()
        {
            if (this.changedByPosMap == null)
            {
                SortedDictionary<int, string> res = new SortedDictionary<int, string>();
                foreach (int pos in changedFields)
                {
                    res[pos] = updates[pos - 1];
                }
                changedByPosMap = ImmutableSortedDictionary.CreateRange(res);
            }

            return changedByPosMap;
        }

        private IDictionary<string, string> allByName()
        {
            if (this.allByNameMap == null)
            {
                SortedDictionary<string, string> res = new SortedDictionary<string, string>(new OrderedFieldNamesComparator(this));
                IEnumerator<string> iterate = updates.GetEnumerator();
                int pos = 1;
                while (iterate.MoveNext())
                {
                    res[fields.getName(pos)] = iterate.Current;
                    pos++;
                }

                allByNameMap = ImmutableSortedDictionary.CreateRange(res);
            }
            return allByNameMap;
        }

        private IDictionary<int, string> allByPos()
        {
            if (this.allByPosMap == null)
            {
                SortedDictionary<int, string> res = new SortedDictionary<int, string>();
                IEnumerator<string> iterate = updates.GetEnumerator();
                int pos = 1;
                while (iterate.MoveNext())
                {
                    res[pos] = iterate.Current;
                    pos++;
                }

                allByPosMap = ImmutableSortedDictionary.CreateRange(res);
            }
            return allByPosMap;
        }

        private class OrderedFieldNamesComparator : IComparer<string>
        {
            private readonly ItemUpdate outerInstance;

            public OrderedFieldNamesComparator(ItemUpdate outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            public virtual int Compare(string field1, string field2)
            {
                return outerInstance.toPos(field1) - outerInstance.toPos(field2);
            }
        }
    }
}