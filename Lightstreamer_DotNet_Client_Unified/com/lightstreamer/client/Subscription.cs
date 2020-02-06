using com.lightstreamer.client.events;
using com.lightstreamer.util;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

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
    using ChangeSubscriptionRequest = com.lightstreamer.client.requests.ChangeSubscriptionRequest;
    using Descriptor = com.lightstreamer.util.Descriptor;
    using ListDescriptor = com.lightstreamer.util.ListDescriptor;
    using NameDescriptor = com.lightstreamer.util.NameDescriptor;
    using Number = com.lightstreamer.util.Number;
    using ProtocolConstants = com.lightstreamer.client.protocol.ProtocolConstants;
    using SessionThread = com.lightstreamer.client.session.SessionThread;
    using SubscribeRequest = com.lightstreamer.client.requests.SubscribeRequest;
    using SubscriptionListenerClearSnapshotEvent = com.lightstreamer.client.events.SubscriptionListenerClearSnapshotEvent;
    using SubscriptionListenerCommandSecondLevelItemLostUpdatesEvent = com.lightstreamer.client.events.SubscriptionListenerCommandSecondLevelItemLostUpdatesEvent;
    using SubscriptionListenerCommandSecondLevelSubscriptionErrorEvent = com.lightstreamer.client.events.SubscriptionListenerCommandSecondLevelSubscriptionErrorEvent;
    using SubscriptionListenerConfigurationEvent = com.lightstreamer.client.events.SubscriptionListenerConfigurationEvent;
    using SubscriptionListenerEndEvent = com.lightstreamer.client.events.SubscriptionListenerEndEvent;
    using SubscriptionListenerEndOfSnapshotEvent = com.lightstreamer.client.events.SubscriptionListenerEndOfSnapshotEvent;
    using SubscriptionListenerItemLostUpdatesEvent = com.lightstreamer.client.events.SubscriptionListenerItemLostUpdatesEvent;
    using SubscriptionListenerItemUpdateEvent = com.lightstreamer.client.events.SubscriptionListenerItemUpdateEvent;
    using SubscriptionListenerStartEvent = com.lightstreamer.client.events.SubscriptionListenerStartEvent;
    using SubscriptionListenerSubscriptionErrorEvent = com.lightstreamer.client.events.SubscriptionListenerSubscriptionErrorEvent;
    using SubscriptionListenerSubscriptionEvent = com.lightstreamer.client.events.SubscriptionListenerSubscriptionEvent;
    using SubscriptionListenerUnsubscriptionEvent = com.lightstreamer.client.events.SubscriptionListenerUnsubscriptionEvent;

    /// <summary>
    /// Class representing a Subscription to be submitted to a Lightstreamer Server. It contains 
    /// subscription details and the listeners needed to process the real-time data. <br/>
    /// After the creation, a Subscription object is in the "inactive" state. When a Subscription 
    /// object is subscribed to on a LightstreamerClient object, through the 
    /// <seealso cref="LightstreamerClient.subscribe(Subscription)"/> method, its state becomes "active". 
    /// This means that the client activates a subscription to the required items through 
    /// Lightstreamer Server and the Subscription object begins to receive real-time events. <br/>
    /// A Subscription can be configured to use either an Item Group or an Item List to specify the 
    /// items to be subscribed to and using either a Field Schema or Field List to specify the fields. <br/>
    /// "Item Group" and "Item List" are defined as follows:
    /// <ul>
    ///  <li>"Item Group": an Item Group is a String identifier representing a list of items. 
    ///  Such Item Group has to be expanded into a list of items by the getItems method of the 
    ///  MetadataProvider of the associated Adapter Set. When using an Item Group, items in the 
    ///  subscription are identified by their 1-based index within the group.<br/>
    ///  It is possible to configure the Subscription to use an "Item Group" leveraging the 
    ///  <seealso cref="Subscription.ItemGroup"/> property.</li> 
    ///  <li>"Item List": an Item List is an array of Strings each one representing an item. 
    ///  For the Item List to be correctly interpreted a LiteralBasedProvider or a MetadataProvider 
    ///  with a compatible implementation of getItems has to be configured in the associated 
    ///  Adapter Set.<br/>
    ///  Note that no item in the list can be empty, can contain spaces or can be a number.<br/>
    ///  When using an Item List, items in the subscription are identified by their name or 
    ///  by their 1-based index within the list.<br/>
    ///  It is possible to configure the Subscription to use an "Item List" leveraging the 
    ///  <seealso cref="Subscription.Items"/> property or by specifying it in the constructor.</li>
    /// </ul>
    /// "Field Schema" and "Field List" are defined as follows:
    /// <ul>
    ///  <li>"Field Schema": a Field Schema is a String identifier representing a list of fields. 
    ///  Such Field Schema has to be expanded into a list of fields by the getFields method of 
    ///  the MetadataProvider of the associated Adapter Set. When using a Field Schema, fields 
    ///  in the subscription are identified by their 1-based index within the schema.<br/>
    ///  It is possible to configure the Subscription to use a "Field Schema" leveraging the 
    ///  <seealso cref="Subscription.FieldSchema"/> property.</li>
    ///  <li>"Field List": a Field List is an array of Strings each one representing a field. 
    ///  For the Field List to be correctly interpreted a LiteralBasedProvider or a MetadataProvider 
    ///  with a compatible implementation of getFields has to be configured in the associated 
    ///  Adapter Set.<br/>
    ///  Note that no field in the list can be empty or can contain spaces.<br/>
    ///  When using a Field List, fields in the subscription are identified by their name or 
    ///  by their 1-based index within the list.<br/>
    ///  It is possible to configure the Subscription to use a "Field List" leveraging the 
    ///  <seealso cref="Subscription.Fields"/> property or by specifying it in the constructor.</li>
    /// </ul>
    /// </summary>
    public class Subscription
    {
        private const string NO_ITEMS = "Please specify a valid item or item list";
        private const string NO_FIELDS = "Invalid Subscription, please specify a field list or field schema";
        private const string IS_ALIVE = "Cannot modify an active Subscription, please unsubscribe before applying any change";
        private const string NOT_ALIVE = "Subscription is not active";
        private const string INVALID_MODE = "The given value is not a valid subscription mode. Admitted values are MERGE, DISTINCT, RAW, COMMAND";
        private const string NO_VALID_FIELDS = "Please specify a valid field list";
        private const string NO_GROUP_NOR_LIST = "The  item list/item group of this Subscription was not initiated";
        private const string NO_SCHEMA_NOR_LIST = "The field list/field schema of this Subscription was not initiated";
        private const string MAX_BUF_EXC = "The given value is not valid for this setting; use null, 'unlimited' or a positive integer instead";
        private const string NO_SECOND_LEVEL = "Second level field list is only available on COMMAND Subscriptions";
        private const string NO_COMMAND = "This method can only be used on COMMAND subscriptions";
        private const string NO_SUB_SCHEMA_NOR_LIST = "The second level of this Subscription was not initiated";
        private const string RAW_NO_SNAPSHOT = "Snapshot is not permitted if RAW was specified as mode";
        private const string NUMERIC_DISTINCT_ONLY = "Numeric values are only allowed when the subscription mode is DISTINCT";
        private const string REQ_SNAP_EXC = "The given value is not valid for this setting; use null, 'yes', 'no' or a positive number instead";
        private const string ILLEGAL_FREQ_EXC = "Can't change the frequency from/to 'unfiltered' or to null while the Subscription is active";
        private const string MAX_FREQ_EXC = "The given value is not valid for this setting; use null, 'unlimited', 'unfiltered' or a positive number instead";
        private const string INVALID_SECOND_LEVEL_KEY = "The received key value is not a valid name for an Item";

        private const string SIMPLE = "SIMPLE";
        private const string METAPUSH = "METAPUSH";
        private const string MULTIMETAPUSH = "MULTIMETAPUSH";

        private const string OFF = "OFF";
        private const string WAITING = "WAITING";
        private const string PAUSED = "PAUSED";
        private const string SUBSCRIBING = "SUBSCRIBING";
        private const string PUSHING = "PUSHING";

        internal const int FREQUENCY_NULL = -2;
        internal const int FREQUENCY_UNFILTERED = -1;
        internal const int FREQUENCY_UNLIMITED = 0;

        internal const int BUFFER_NULL = -1;
        internal const int BUFFER_UNLIMITED = 0;

        private const bool CLEAN = true;
        private const bool DONT_CLEAN = false;


        private readonly ILogger log = LogManager.GetLogger(Constants.ACTIONS_LOG);

        private EventDispatcher<SubscriptionListener> dispatcher = new EventDispatcher<SubscriptionListener>(LightstreamerClient.eventsThread);

        private bool isActive = false;

        internal Descriptor itemDescriptor;
        internal Descriptor fieldDescriptor;
        private int commandCode = -1;
        private int keyCode = -1;

        private int nextReconfId = 1;

        //data
        private string dataAdapter = null;
        private string mode = null;
        private string isRequiredSnapshot = null;
        private string selector = null;
        internal int requestedBufferSize = BUFFER_NULL;
        private ConcurrentMatrix<int, int> oldValuesByItem = new ConcurrentMatrix<int, int>(); //concurrent to handle getValue calls
        private ConcurrentMatrix<string, int> oldValuesByKey = new ConcurrentMatrix<string, int>(); //concurrent to handle getValue calls
                                                                                                    //2nd level data
        private string underDataAdapter = null;
        private Descriptor subFieldDescriptor;
        private Matrix<int, string, Subscription> subTables = new Matrix<int, string, Subscription>();
        //completely useless (may directly use the constant)
        //I save it here so that I can modify it to DISTINCT in the SubscriptionUnusualEvents test.
        protected internal string subMode = Constants.MERGE;
        private double aggregatedRealMaxFrequency = FREQUENCY_NULL;
        private bool subTableFlag = false;

        private string behavior = null;
        internal double requestedMaxFrequency = FREQUENCY_NULL;
        private double localRealMaxFrequency = FREQUENCY_NULL;
        // NOTE: used only for two-level subscriptions (for both first and second level)
        // not needed for normal subscriptions, where the information reaches the listener directly 
        private int subscriptionId = -1;

        private string tablePhaseType = OFF;
        private int tablePhase = 0;
        private SubscriptionManager manager;
        private SessionThread sessionThread;
        private SnapshotManager snapshotManager;

        /// <summary>
        /// Creates an object to be used to describe a Subscription that is going to be subscribed to 
        /// through Lightstreamer Server. The object can be supplied to 
        /// <seealso cref="LightstreamerClient.subscribe(Subscription)"/> and 
        /// <seealso cref="LightstreamerClient.unsubscribe(Subscription)"/>, in order to bring the Subscription 
        /// to "active" or back to "inactive" state. <br/>
        /// Note that all of the methods used to describe the subscription to the server can only be 
        /// called while the instance is in the "inactive" state; the only exception is 
        /// <seealso cref="Subscription.RequestedMaxFrequency"/>.
        /// </summary>
        /// <param name="subscriptionMode"> the subscription mode for the items, required by Lightstreamer Server. 
        /// Permitted values are:
        /// <ul>
        ///  <li>MERGE</li>
        ///  <li>DISTINCT</li>
        ///  <li>RAW</li>
        ///  <li>COMMAND</li>
        /// </ul> </param>
        /// <param name="items"> an array of items to be subscribed to through Lightstreamer server. <br/>
        /// It is also possible specify the "Item List" or "Item Group" later through 
        /// <seealso cref="Subscription.Items"/> and <seealso cref="Subscription.ItemGroup"/>. </param>
        /// <param name="fields"> an array of fields for the items to be subscribed to through Lightstreamer Server. <br/>
        /// It is also possible to specify the "Field List" or "Field Schema" later through 
        /// <seealso cref="Subscription.Fields"/> and <seealso cref="Subscription.FieldSchema"/>.
        /// </param>
        public Subscription(string subscriptionMode, string[] items, string[] fields)
        {
            //Please specify a valid item or item list
            //Please specify a valid field list

            init(subscriptionMode, items, fields);
        }

        /// <summary>
        /// Creates an object to be used to describe a Subscription that is going to be subscribed to 
        /// through Lightstreamer Server. The object can be supplied to 
        /// <seealso cref="LightstreamerClient.subscribe(Subscription)"/> and 
        /// <seealso cref="LightstreamerClient.unsubscribe(Subscription)"/>, in order to bring the Subscription 
        /// to "active" or back to "inactive" state. <br/>
        /// Note that all of the methods used to describe the subscription to the server can only be 
        /// called while the instance is in the "inactive" state; the only exception is 
        /// <seealso cref="Subscription.RequestedMaxFrequency"/>.
        /// </summary>
        /// <param name="subscriptionMode"> the subscription mode for the items, required by Lightstreamer Server. 
        /// Permitted values are:
        /// <ul>
        ///  <li>MERGE</li>
        ///  <li>DISTINCT</li>
        ///  <li>RAW</li>
        ///  <li>COMMAND</li>
        /// </ul> </param>
        /// <param name="item"> the item name to be subscribed to through Lightstreamer Server. </param>
        /// <param name="fields"> an array of fields for the items to be subscribed to through Lightstreamer Server. <br/>
        /// It is also possible to specify the "Field List" or "Field Schema" later through 
        /// <seealso cref="Subscription.Fields"/> and <seealso cref="Subscription.FieldSchema"/>.
        /// </param>
        public Subscription(string subscriptionMode, string item, string[] fields)
        {
            //Please specify a valid item or item list
            //Please specify a valid field list

            init(subscriptionMode, new string[] { item }, fields);
        }

        /// <summary>
        /// Creates an object to be used to describe a Subscription that is going to be subscribed to 
        /// through Lightstreamer Server. The object can be supplied to 
        /// <seealso cref="LightstreamerClient.subscribe(Subscription)"/> and 
        /// <seealso cref="LightstreamerClient.unsubscribe(Subscription)"/>, in order to bring the Subscription 
        /// to "active" or back to "inactive" state. <br/>
        /// Note that all of the methods used to describe the subscription to the server can only be 
        /// called while the instance is in the "inactive" state; the only exception is 
        /// <seealso cref="Subscription.RequestedMaxFrequency"/>.
        /// </summary>
        /// <param name="subscriptionMode"> the subscription mode for the items, required by Lightstreamer Server. 
        /// Permitted values are:
        /// <ul>
        ///  <li>MERGE</li>
        ///  <li>DISTINCT</li>
        ///  <li>RAW</li>
        ///  <li>COMMAND</li>
        /// </ul> </param>
        public Subscription(string subscriptionMode)
        {
            init(subscriptionMode, null, null);
        }

        private void init(string subscriptionMode, string[] items, string[] fields)
        {
            if (string.ReferenceEquals(subscriptionMode, null))
            {
                throw new System.ArgumentException(INVALID_MODE);
            }

            subscriptionMode = subscriptionMode.ToUpper();
            if (!Constants.MODES.Contains(subscriptionMode))
            {
                throw new System.ArgumentException(INVALID_MODE);
            }

            this.mode = subscriptionMode;

            //DEFAULT: "yes" if mode is MERGE, DISTINCT, or COMMAND; null if mode is RAW 
            this.isRequiredSnapshot = subscriptionMode.Equals(Constants.RAW) ? null : "yes";

            this.behavior = this.mode.Equals(Constants.COMMAND) ? METAPUSH : SIMPLE;

            /////////////////Setup   
            if (items != null)
            {
                if (fields == null)
                {
                    throw new System.ArgumentException(NO_VALID_FIELDS);
                }
                this.Items = items;
                this.Fields = fields;
            }
            else if (fields != null)
            {
                throw new System.ArgumentException(NO_ITEMS);
            }
        }

        /// <summary>
        /// Adds a listener that will receive events from the Subscription instance. <br/> 
        /// The same listener can be added to several different Subscription instances.
        /// 
        /// <b>Lifecycle:</b>  A listener can be added at any time. A call to add a listener already 
        /// present will be ignored.
        /// </summary>
        /// <param name="listener"> An object that will receive the events as documented in the 
        /// SubscriptionListener interface.
        /// </param>
        /// <seealso cref="Subscription.removeListener(SubscriptionListener)" />
        public virtual void addListener(SubscriptionListener listener)
        {
            lock (this)
            {
                this.dispatcher.AddListener(listener, new SubscriptionListenerStartEvent(this));
            }
        }

        /// <summary>
        /// Removes a listener from the Subscription instance so that it will not receive 
        /// events anymore.
        /// 
        /// <b>Lifecycle:</b>  a listener can be removed at any time.
        /// </summary>
        /// <param name="listener"> The listener to be removed.
        /// </param>
        /// <seealso cref="Subscription.addListener(SubscriptionListener)" />
        public virtual void removeListener(SubscriptionListener listener)
        {
            lock (this)
            {
                this.dispatcher.removeListener(listener, new SubscriptionListenerEndEvent(this));
            }
        }

        /// <summary>
        /// Returns a list containing the <seealso cref="SubscriptionListener"/> instances that were 
        /// added to this client. </summary>
        /// <returns> a list containing the listeners that were added to this client. </returns>
        /// <seealso cref="Subscription.addListener(SubscriptionListener)" />
        public virtual IList<SubscriptionListener> Listeners
        {
            get
            {
                lock (this)
                {
                    return this.dispatcher.Listeners;
                }
            }
        }

        /// <value>
        /// Read-only property <c>Active</c> checks if the Subscription is currently "active" or not.
        /// Most of the Subscription properties cannot be modified if a Subscription is "active".<br/>
        /// The status of a Subscription is changed to "active" through the  
        /// <seealso cref="LightstreamerClient.subscribe(Subscription)"/> method and back to 
        /// "inactive" through the <seealso cref="LightstreamerClient.unsubscribe(Subscription)"/> one.<br/>
        /// Returns true/false if the Subscription is "active" or not.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time.
        /// </value>
        /// <seealso cref="LightstreamerClient.subscribe(Subscription)" />
        /// <seealso cref="LightstreamerClient.unsubscribe(Subscription)" />
        public virtual bool Active
        {
            get
            {
                lock (this)
                {
                    return this.isActive;
                }
            }
        }

        /// <value>
        /// Read-only property <c>Subscribed</c> thtat checks if the Subscription is currently subscribed
        /// to through the server or not.<br/>
        /// This flag is switched to true by server sent Subscription events, and 
        /// back to false in case of client disconnection, 
        /// <seealso cref="LightstreamerClient.unsubscribe(Subscription)"/> calls and server 
        /// sent unsubscription events.<br/>
        /// Returns true/false if the Subscription is subscribed to through the server or not.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time.
        /// </value>
        public virtual bool Subscribed
        {
            get
            {
                lock (this)
                {
                    return this.@is(PUSHING); //might change while we check, no need to worry
                }
            }
        }

        /// <value>
        /// Property <c>DataAdapter</c> represents the name of the Data Adapter (within the Adapter Set used by the current session)
        /// that supplies all the items for this Subscription.<br/>
        /// The Data Adapter name is configured on the server side through the "name" attribute of the
        /// "data_provider" element, in the "adapters.xml" file that defines the Adapter Set (a missing
        /// attribute configures the "DEFAULT" name).<br/>
        /// Note that if more than one Data Adapter is needed to supply all the items in a set of items, then
        /// it is not possible to group all the items of the set in a single Subscription. Multiple
        /// Subscriptions have to be defined.<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in its
        /// "inactive" state.<br/>
        /// <br/>
        /// <b>Default value:</b> The default Data Adapter for the Adapter Set, configured as "DEFAULT" on the Server.
        /// </value>
        public virtual string DataAdapter
        {
            get
            {
                lock (this)
                {
                    return this.dataAdapter;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    this.dataAdapter = value;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Adapter Set assigned: " + value);
                    }
                }
            }
        }

        /// <value>
        /// Read-only property <c>Mode</c> represents the mode specified in the constructor for
        /// this Subscription.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time.
        /// </value>
        public virtual string Mode
        {
            get
            {
                lock (this)
                {
                    return this.mode;
                }
            }
        }

        /// <value>
        /// Property <c>Items</c> represents the "Item List"  to be subscribed to through Lightstreamer Server.
        /// Any call to set this property will override any "Item List" or "Item Group" previously specified.
        /// Note that if the single-item-constructor was used, this method will return an array 
        /// of length 1 containing such item.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can only be called if the Subscription has been initialized 
        /// with an "Item List".
        /// </value>
        public virtual string[] Items
        {
            get
            {
                lock (this)
                {

                    if (this.itemDescriptor == null)
                    {
                        throw new System.InvalidOperationException(NO_GROUP_NOR_LIST);
                    }
                    else if (this.itemDescriptor is NameDescriptor)
                    {
                        throw new System.InvalidOperationException("This Subscription was initiated using an item group, use getItemGroup instead of using getItems");
                    }


                    return ( (ListDescriptor)this.itemDescriptor ).Original;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    ListDescriptor.checkItemNames(value, "An item");

                    this.itemDescriptor = value == null ? null : new ListDescriptor(value);

                    debugDescriptor("Item list assigned: ", this.itemDescriptor);
                }
            }
        }

        /// <value>
        /// Property <c>ItemGroup</c> represents the the "Item Group" to be subscribed to through
        /// Lightstreamer Server.
        /// Any call to set this property will override any "Item List" or "Item Group" previously specified.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can only be called if the Subscription has been initialized
        /// using an "Item Group".
        /// </value>
        public virtual string ItemGroup
        {
            get
            {
                lock (this)
                {
                    if (this.itemDescriptor == null)
                    {
                        throw new System.InvalidOperationException(NO_GROUP_NOR_LIST);
                    }
                    else if (this.itemDescriptor is ListDescriptor)
                    {
                        throw new System.InvalidOperationException("This Subscription was initiated using an item list, use getItems instead of using getItemGroup");
                    }


                    return ( (NameDescriptor)this.itemDescriptor ).Original;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    this.itemDescriptor = string.ReferenceEquals(value, null) ? null : new NameDescriptor(value);

                    debugDescriptor("Item group assigned: ", this.itemDescriptor);
                }
            }
        }

        /// <value>
        /// Property <c>Fields</c> represents the "Field  List"  to be subscribed to through Lightstreamer Server.
        /// Any call to set this property will override any "Field  List" or "Field Schema" previously specified.<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This property can be set only while the Subscription instance is in its "inactive" state.
        /// </value>
        public virtual string[] Fields
        {
            get
            {
                lock (this)
                {
                    if (this.fieldDescriptor == null)
                    {
                        throw new System.InvalidOperationException(NO_SCHEMA_NOR_LIST);
                    }
                    else if (this.fieldDescriptor is NameDescriptor)
                    {
                        throw new System.InvalidOperationException("This Subscription was initiated using a field schema, use getFieldSchema instead of using getFields");
                    }

                    return ( (ListDescriptor)this.fieldDescriptor ).Original;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    ListDescriptor.checkFieldNames(value, "A field");

                    Descriptor tmp = value == null ? null : new ListDescriptor(value);

                    this.SchemaReadMetapushFields = tmp;

                    this.fieldDescriptor = tmp;

                    debugDescriptor("Field list assigned: ", fieldDescriptor);
                }
            }
        }

        /// <value>
        /// Property <c>FieldSchema</c> represents the "Field Schema" to be subscribed to through Lightstreamer Server.
        /// Any call to set this property will override any "Field  List" or "Field Schema" previously specified.<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This property can be set only while the Subscription instance is in its "inactive" state.
        /// </value>
        public virtual string FieldSchema
        {
            get
            {
                lock (this)
                {
                    if (this.fieldDescriptor == null)
                    {
                        throw new System.InvalidOperationException(NO_SCHEMA_NOR_LIST);
                    }
                    else if (this.fieldDescriptor is ListDescriptor)
                    {
                        throw new System.InvalidOperationException("This Subscription was initiated using a field schema, use getFieldSchema instead of using getFields");
                    }

                    return ( (NameDescriptor)this.fieldDescriptor ).Original;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    this.fieldDescriptor = string.ReferenceEquals(value, null) ? null : new NameDescriptor(value);

                    debugDescriptor("Field group assigned: ", this.fieldDescriptor);
                }
            }
        }

        /// <value>
        /// Property <c>RequestedBufferSize</c> represents the length to be requested to Lightstreamer Server
        /// for the internal queuing buffers for the items in the Subscription. A Queuing buffer is used by
        /// the Server to accumulate a burst of updates for an item, so that they can all be sent to the
        /// client, despite of bandwidth or frequency limits. It can be used only when the subscription mode
        /// is MERGE or DISTINCT and unfiltered dispatching has not been requested. Note that the Server may
        /// pose an upper limit on the size of its internal buffers.<br/>
        /// The value of this property is integer number, representing the length of the internal queuing
        /// buffers to be used in the Server. If the string "unlimited" is supplied, then no buffer size
        /// limit is requested (the check is case insensitive). It is also possible to supply a null value
        /// to stick to the Server default (which currently depends on the subscription mode).<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in its
        /// "inactive" state.<br/>
        /// <br/>
        /// <b>Default value:</b> null, meaning to lean on the Server default based on the subscription mode.
        /// This means that the buffer size will be 1 for MERGE subscriptions and "unlimited" for DISTINCT
        /// subscriptions. See the "General Concepts" document for further details.
        /// </value>
        public virtual string RequestedBufferSize
        {
            get
            {
                lock (this)
                {
                    if (this.requestedBufferSize == BUFFER_NULL)
                    {
                        return null;
                    }
                    else if (this.requestedBufferSize == BUFFER_UNLIMITED)
                    {
                        return "unlimited";
                    }
                    return this.requestedBufferSize.ToString();
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    //can be
                    //null -> make it -1
                    //unlimited -> make it 0
                    //>0 integer

                    if (string.ReferenceEquals(value, null))
                    {
                        this.requestedBufferSize = BUFFER_NULL;
                    }
                    else
                    {
                        string lvalue = value;
                        value = toLowerCase(value);
                        if (value.Equals("unlimited"))
                        {
                            this.requestedBufferSize = BUFFER_UNLIMITED;
                        }
                        else
                        {
                            int tmp;
                            try
                            {
                                tmp = int.Parse(lvalue);
                            }
                            catch (System.FormatException nfe)
                            {
                                throw new System.ArgumentException(MAX_BUF_EXC, nfe);
                            }

                            if (!Number.isPositive(tmp, Number.DONT_ACCEPT_ZERO))
                            {
                                throw new System.ArgumentException(MAX_BUF_EXC);
                            }
                            this.requestedBufferSize = tmp;
                        }
                    }

                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Requested Buffer Size assigned: " + this.requestedBufferSize);
                    }
                }
            }
        }

        /// <value>
        /// Property <c>RequestedSnapshot</c> enables/disables snapshot delivery request for the items in
        /// the Subscription. The snapshot can be requested only if the Subscription mode is
        /// MERGE, DISTINCT or COMMAND.
        /// The value can be "yes"/"no" to request/not request snapshot delivery (the check is case insensitive).
        /// If the Subscription mode is DISTINCT, instead of "yes", it is also possible to supply an integer
        /// number, to specify the requested length of the snapshot (though the length of the received
        /// snapshot may be less than requested, because of insufficient data or server side limits);
        /// passing "yes" means that the snapshot length should be determined only by the Server. Null is
        /// also a valid value; if specified, no snapshot preference will be sent to the server that will
        /// decide itself whether or not to send any snapshot.<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in its "inactive" state.<br/>
        /// <br/>
        /// <b>Default value:</b> "yes" if the Subscription mode is not "RAW", null otherwise.
        /// </value>
        public virtual string RequestedSnapshot
        {
            get
            {
                lock (this)
                {
                    return this.isRequiredSnapshot;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    if (!string.ReferenceEquals(value, null))
                    {
                        value = toLowerCase(value);

                        if (value.Equals("no"))
                        {
                            //the string "no" - admitted for all modes     
                        }
                        else if (this.mode.Equals(Constants.RAW))
                        {
                            //RAW does not accept anything else
                            throw new System.InvalidOperationException(RAW_NO_SNAPSHOT);

                        }
                        else if (value.Equals("yes"))
                        {
                            //the string "yes" - admitted for MERGE, DISTINCT, COMMAND modes
                        }
                        else if (Number.isNumber(value))
                        {
                            //a String to be parsed as a >0 int - admitted for DISTINCT mode

                            if (!this.mode.Equals(Constants.DISTINCT))
                            {
                                throw new System.InvalidOperationException(NUMERIC_DISTINCT_ONLY);
                            }

                            int tmp;
                            try
                            {
                                tmp = int.Parse(value);
                            }
                            catch (System.FormatException nfe)
                            {
                                throw new System.ArgumentException(REQ_SNAP_EXC, nfe);
                            }

                            if (!Number.isPositive(tmp, Number.DONT_ACCEPT_ZERO))
                            {
                                throw new System.ArgumentException(REQ_SNAP_EXC);
                            }

                        }
                        else
                        {
                            throw new System.ArgumentException(REQ_SNAP_EXC);
                        }
                    }

                    this.isRequiredSnapshot = value;

                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Snapshot Required assigned: " + this.isRequiredSnapshot);
                    }
                }
            }
        }

        /// <value>
        /// Property <c>RequestedMaxFrequency</c> represents the maximum update frequency to be requested to
        /// Lightstreamer Server for all the items in the Subscription. It can be used only if the
        /// Subscription mode is MERGE, DISTINCT or COMMAND (in the latter case, the frequency limitation
        /// applies to the UPDATE events for each single key). For Subscriptions with two-level behavior
        /// (see <seealso cref="Subscription.CommandSecondLevelFields"/> and 
        /// <seealso cref="Subscription.CommandSecondLevelFieldSchema"/>,
        /// the specified frequency limit applies to both first-level and second-level items.<br/>
        /// Note that frequency limits on the items can also be set on the server side and this request can
        /// only be issued in order to furtherly reduce the frequency, not to rise it beyond these limits.<br/>
        /// This method can also be used to request unfiltered dispatching for the items in the Subscription.
        /// However, unfiltered dispatching requests may be refused if any frequency limit is posed on the
        /// server side for some item.<br/>
        /// The value can be a decimal number, representing the maximum update frequency (expressed in updates per
        /// second) for each item in the Subscription; for instance, with a setting of 0.5, for each single
        /// item, no more than one update every 2 seconds will be received. If the string "unlimited" is
        /// supplied, then no frequency limit is requested. It is also possible to supply the string
        /// "unfiltered", to ask for unfiltered dispatching, if it is allowed for the items, or a null value
        /// to stick to the Server default (which currently corresponds to "unlimited"). The check for the
        /// string constants is case insensitive.<br/>
        /// <br/>
        /// <b>Edition Note:</b> A further global frequency limit could also be imposed by the Server,
        /// depending on Edition and License Type; this specific limit also applies to RAW mode and to
        /// unfiltered dispatching.
        /// To know what features are enabled by your license, please see the License tab of the Monitoring Dashboard (by default,
        /// available at /dashboard).<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can can be called at any time with some differences based on the
        /// Subscription status:
        /// <ul>
        /// <li>If the Subscription instance is in its "inactive" state then this method can be called at will.</li>
        /// <li>If the Subscription instance is in its "active" state then the method can still be called
        /// unless the current value is "unfiltered" or the supplied value is "unfiltered" or null. If the
        /// Subscription instance is in its "active" state and the connection to the server is currently open,
        /// then a request to change the frequency of the Subscription on the fly is sent to the server.</li>
        /// </ul>
        /// <br/>
        /// <b>Default value:</b> null, meaning to lean on the Server default based on the subscription mode.
        /// This consists, for all modes, in not applying any frequency limit to the subscription (the same
        /// as "unlimited"); see the "General Concepts" document for further details.
        /// </value>
        public virtual string RequestedMaxFrequency
        {
            get
            {
                lock (this)
                {
                    if (this.requestedMaxFrequency == FREQUENCY_UNFILTERED)
                    {
                        return "unfiltered";
                    }
                    else if (this.requestedMaxFrequency == FREQUENCY_NULL)
                    {
                        return null;
                    }
                    else if (this.requestedMaxFrequency == FREQUENCY_UNLIMITED)
                    {
                        return "unlimited";
                    }
                    else
                    {
                        return this.requestedMaxFrequency.ToString();
                    }
                }
            }
            set
            {
                lock (this)
                {
                    //can be:
                    //null -> do not send to the server -> -2
                    //unfiltered -> -1
                    //unlimited -> 0
                    //>0 double

                    double prev = this.requestedMaxFrequency;

                    if (!string.ReferenceEquals(value, null))
                    {
                        value = value.ToLower();
                    }

                    //double orig = this.requestedMaxFrequency;
                    if (this.Active)
                    {
                        if (string.ReferenceEquals(value, null))
                        {
                            //null was given
                            throw new System.InvalidOperationException(ILLEGAL_FREQ_EXC);
                        }
                        else if (value.Equals("unfiltered") || this.requestedMaxFrequency == FREQUENCY_UNFILTERED)
                        {
                            //currently unfiltered or unfiltered was given
                            throw new System.InvalidOperationException(ILLEGAL_FREQ_EXC);
                        }
                    }

                    if (string.ReferenceEquals(value, null))
                    {
                        this.requestedMaxFrequency = FREQUENCY_NULL;
                    }
                    else if (value.Equals("unfiltered"))
                    {
                        this.requestedMaxFrequency = FREQUENCY_UNFILTERED;
                    }
                    else if (value.Equals("unlimited"))
                    {
                        this.requestedMaxFrequency = FREQUENCY_UNLIMITED;
                    }
                    else
                    {
                        double tmp;
                        try
                        {
                            tmp = double.Parse(value, CultureInfo.InvariantCulture);
                        }
                        catch (System.FormatException nfe)
                        {
                            throw new System.ArgumentException(MAX_FREQ_EXC, nfe);
                        }

                        if (!Number.isPositive(tmp, Number.DONT_ACCEPT_ZERO))
                        {
                            throw new System.ArgumentException(MAX_FREQ_EXC);
                        }

                        this.requestedMaxFrequency = tmp;
                    }

                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Requested Max Frequency assigned: " + this.requestedMaxFrequency);
                    }

                    if (sessionThread == null)
                    { //onAdd event not yet fired, exit: the frequency will be correct from the beginning
                        return;
                    }

                    string frequency = value;
                    Subscription that = this;
                    sessionThread.queue(new System.Threading.Tasks.Task(() =>
                  {

                        //when this executes the frequency might be changed again, in that case this request will be aborted. We might skip a request altogether when that's the case

                        if (@is(WAITING) || @is(SUBSCRIBING) || @is(PUSHING))
                      {
                            //our subscription request was already forwarded to the Session machine, let's send a follow-up
                            if (prev != requestedMaxFrequency)
                          {
                                //the frequency actually changed
                                manager.changeFrequency(that);

                                //might happen that we send a useless request

                                if (behavior.Equals(MULTIMETAPUSH))
                              {


                                  subTables.forEachElement(new ElementCallbackAnonymousInnerClassS(frequency));




                              }
                          }
                      }
                  }));



                }
            }
        }

        private class ElementCallbackAnonymousInnerClassS : Matrix<int, string, Subscription>.ElementCallback<int, string, Subscription>
        {

            private String frequency;

            public ElementCallbackAnonymousInnerClassS(String freq)
            {
                this.frequency = freq;
            }

            public bool onElement(Subscription secondLevelSubscription, int? item, string key)
            {
                secondLevelSubscription.RequestedMaxFrequency = frequency;
                return false;
            }

            public Boolean onElement(Subscription secondLevelSubscription, int item, String key)
            {
                secondLevelSubscription.RequestedMaxFrequency = frequency;
                return false;
            }
        }

        /// <value>
        /// Property <c>Selector</c> represents the selector name for all the items in the Subscription.
        /// The selector is a filter on the updates received. It is executed on the Server and implemented
        /// by the Metadata Adapter.<br/>
        /// The name of a selector should be recognized by the Metadata Adapter, or can be null to unset
        /// the selector.<br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in its
        /// "inactive" state.<br/>
        /// <br/>
        /// <b>Default value:</b> null (no selector).
        /// </value>
        public virtual string Selector
        {
            get
            {
                lock (this)
                {
                    return this.selector;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();

                    this.selector = value;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Selector assigned: " + value);
                    }
                }
            }
        }

        /// <value>
        /// Read-only property <c>CommandPosition</c> represents the "command" field in a COMMAND
        /// Subscription.<br/>
        /// This method can only be used if the Subscription mode is COMMAND and the Subscription 
        /// was initialized using a "Field Schema".<br/>
        /// The value is the 1-based position of the "command" field within the "Field Schema".<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time after the first 
        /// <seealso cref="SubscriptionListener.onSubscription"/> event.
        /// </value>
        public virtual int CommandPosition
        {
            get
            {
                lock (this)
                {
                    this.commandCheck();

                    if (this.fieldDescriptor is ListDescriptor)
                    {
                        throw new System.InvalidOperationException("This Subscription was initiated using a field list, command field is always 'command'");
                    }

                    if (this.commandCode == -1)
                    {
                        throw new System.InvalidOperationException("The position of the command field is currently unknown");
                    }

                    return this.commandCode;
                }
            }
        }

        /// <value>
        /// Read-only property <c>KeyPosition</c> represents the position of the "key" field in a
        /// COMMAND Subscription.<br/>
        /// This method can only be used if the Subscription mode is COMMAND
        /// and the Subscription was initialized using a "Field Schema".<br/>
        /// The value is the 1-based position of the "key" field within the "Field Schema".<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time.
        /// </value>
        public virtual int KeyPosition
        {
            get
            {
                lock (this)
                {
                    this.commandCheck();

                    if (this.fieldDescriptor is ListDescriptor)
                    {
                        throw new System.InvalidOperationException("This Subscription was initiated using a field list, key field is always 'key'");
                    }

                    if (this.keyCode == -1)
                    {
                        throw new System.InvalidOperationException("The position of the key field is currently unknown");
                    }

                    return this.keyCode;
                }
            }
        }

        /// <value>
        /// Property <c>CommandSecondLevelDataAdapter</c> represents the name of the second-level Data Adapter
        /// (within the Adapter Set used by the current session) that supplies all the second-level items.<br/>
        /// All the possible second-level items should be supplied in "MERGE" mode with snapshot available.<br/>
        /// The Data Adapter name is configured on the server side through the "name" attribute of the 
        /// data_provider element, in the "adapters.xml" file that defines the Adapter Set (a missing
        /// attribute configures the "DEFAULT" name).<br/>
        /// A null value is equivalent to the "DEFAULT" name.<br/>
        /// See also: <seealso cref="Subscription.CommandSecondLevelFields"/>, <seealso cref="Subscription.CommandSecondLevelFieldSchema"/><br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in its
        /// "inactive" state.<br/>
        /// <br/>
        /// <b>Default value:</b> The default Data Adapter for the Adapter Set, configured as "DEFAULT"
        /// on the Server.
        /// </value>
        public virtual string CommandSecondLevelDataAdapter
        {
            get
            {
                lock (this)
                {
                    return this.underDataAdapter;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();
                    this.secondLevelCheck();

                    this.underDataAdapter = value;
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Second level Data Adapter Set assigned: " + value);
                    }
                }
            }
        }

        /// <value>
        /// Property <c>CommandSecondLevelFields</c> represents the "Field List" to be subscribed to
        /// through Lightstreamer Server for the second-level items. It can only be used on COMMAND
        /// Subscriptions.<br/>
        /// Any call to this method will override any "Field List" or "Field Schema" previously specified
        /// for the second-level.<br/>
        /// Calling this method enables the two-level behavior: in synthesis, each time a new key is received
        /// on the COMMAND Subscription, the key value is treated as an Item name and an underlying
        /// Subscription for this Item is created and subscribed to automatically, to feed fields specified
        /// by this method. This mono-item Subscription is specified through an "Item List" containing only
        /// the Item name received. As a consequence, all the conditions provided for subscriptions through
        /// Item Lists have to be satisfied. The item is subscribed to in "MERGE" mode, with snapshot request
        /// and with the same maximum frequency setting as for the first-level items (including the
        /// "unfiltered" case). All other Subscription properties are left as the default. When the key is
        /// deleted by a DELETE command on the first-level Subscription, the associated second-level
        /// Subscription is also unsubscribed from.<br/>
        /// Specifying null as parameter will disable the two-level behavior.<br/>
        /// Ensure that no name conflict is generated between first-level and second-level fields. In case
        /// of conflict, the second-level field will not be accessible by name, but only by position.<br/>
        /// See also: <seealso cref="Subscription.CommandSecondLevelFieldSchema"/><br/>
        /// <br/>
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in its
        /// "inactive" state.<br/>
        /// </value>
        public virtual string[] CommandSecondLevelFields
        {
            get
            {
                lock (this)
                {
                    if (this.subFieldDescriptor == null)
                    {
                        throw new System.InvalidOperationException(NO_SUB_SCHEMA_NOR_LIST);
                    }
                    else if (this.subFieldDescriptor is NameDescriptor)
                    {
                        throw new System.InvalidOperationException("The second level of this Subscription was initiated using a field schema, use getCommandSecondLevelFieldSchema instead of using getCommandSecondLevelFields");
                    }

                    return ( (ListDescriptor)this.subFieldDescriptor ).Original;

                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();
                    this.secondLevelCheck();

                    ListDescriptor.checkFieldNames(value, "A field");

                    this.subFieldDescriptor = value == null ? null : new ListDescriptor(value);

                    this.prepareSecondLevel();

                    debugDescriptor("Second level field list assigned: ", this.subFieldDescriptor);
                }
            }
        }

        /// <value>
        /// Property <c>CommandSecondLevelFieldSchema</c> represents the "Field Schema" to be subscribed to
        /// through Lightstreamer Server for the second-level items. It can only be used on
        /// COMMAND Subscriptions.<br/>
        /// Any call to this method will override any "Field List" or "Field Schema" previously specified for
        /// the second-level.<br/>
        /// Calling this method enables the two-level behavior: in synthesis, each time a new key is received
        /// on the COMMAND Subscription, the key value is treated as an Item name and an underlying
        /// Subscription for this Item is created and subscribed to automatically, to feed fields specified
        /// by this method. This mono-item Subscription is specified through an "Item List" containing only
        /// the Item name received. As a consequence, all the conditions provided for subscriptions through
        /// Item Lists have to be satisfied. The item is subscribed to in "MERGE" mode, with snapshot request
        /// and with the same maximum frequency setting as for the first-level items (including the
        /// "unfiltered" case). All other Subscription properties are left as the default. When the key is
        /// deleted by a DELETE command on the first-level Subscription, the associated second-level
        /// Subscription is also unsubscribed from.<br/>
        /// Specify null as parameter will disable the two-level behavior.<br/>
        /// See also: <seealso cref="Subscription.CommandSecondLevelFields"/><br/>
        /// <br/>
        /// 
        /// <b>Lifecycle:</b> This method can only be called while the Subscription instance is in
        /// its "inactive" state.
        /// </value>
        public virtual string CommandSecondLevelFieldSchema
        {
            get
            {
                lock (this)
                {
                    if (this.subFieldDescriptor == null)
                    {
                        throw new System.InvalidOperationException(NO_SUB_SCHEMA_NOR_LIST);
                    }
                    else if (this.subFieldDescriptor is ListDescriptor)
                    {
                        throw new System.InvalidOperationException("The second level of this Subscription was initiated using a field list, use getCommandSecondLevelFields instead of using getCommandSecondLevelFieldSchema");
                    }

                    return ( (NameDescriptor)this.subFieldDescriptor ).Original;
                }
            }
            set
            {
                lock (this)
                {
                    this.notAliveCheck();
                    this.secondLevelCheck();

                    this.subFieldDescriptor = string.ReferenceEquals(value, null) ? null : new NameDescriptor(value);

                    this.prepareSecondLevel();

                    debugDescriptor("Second level field schema assigned: ", this.subFieldDescriptor);
                }
            }
        }

        /// <summary>
        /// Returns the latest value received for the specified item/field pair.<br/>
        /// It is suggested to consume real-time data by implementing and adding
        /// a proper <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// In case of COMMAND Subscriptions, the value returned by this
        /// method may be misleading, as in COMMAND mode all the keys received, being
        /// part of the same item, will overwrite each other; for COMMAND Subscriptions,
        /// use <seealso cref="Subscription.getCommandValue(string, string, string)"/> instead.<br/>
        /// Note that internal data is cleared when the Subscription is 
        /// unsubscribed from.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time; if called 
        /// to retrieve a value that has not been received yet, then it will return null.
        /// </summary>
        /// <param name="itemName"> an item in the configured "Item List" </param>
        /// <param name="fieldName"> a field in the configured "Field List" </param>
        /// <returns> the current value for the specified field of the specified item
        /// (possibly null), or null if no value has been received yet.
        /// </returns>
        public virtual string getValue(string itemName, string fieldName)
        {
            lock (this)
            {
                return this.oldValuesByItem.get(this.toItemPos(itemName).Value, this.toFieldPos(fieldName).Value);
            }
        }

        /// <summary>
        /// Returns the latest value received for the specified item/field pair.<br/>
        /// It is suggested to consume real-time data by implementing and adding
        /// a proper <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// In case of COMMAND Subscriptions, the value returned by this
        /// method may be misleading, as in COMMAND mode all the keys received, being
        /// part of the same item, will overwrite each other; for COMMAND Subscriptions,
        /// use <seealso cref="Subscription.getCommandValue(int, string, int)"/> instead. <br/>
        /// Note that internal data is cleared when the Subscription is 
        /// unsubscribed from.<br/>
        /// Returns null if no value has been received yet for the specified item/field pair.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time; if called 
        /// to retrieve a value that has not been received yet, then it will return null.
        /// </summary>
        /// <param name="itemPos"> the 1-based position of an item within the configured "Item Group"
        /// or "Item List" </param>
        /// <param name="fieldPos"> the 1-based position of a field within the configured "Field Schema"
        /// or "Field List" </param>
        /// <returns> the current value for the specified field of the specified item
        /// (possibly null), or null if no value has been received yet.
        /// </returns>
        public virtual string getValue(int itemPos, int fieldPos)
        {
            lock (this)
            {
                this.verifyItemPos(itemPos);
                this.verifyFieldPos(fieldPos, false);
                return this.oldValuesByItem.get(itemPos, fieldPos);
            }
        }
        /// <summary>
        /// Returns the latest value received for the specified item/field pair.<br/>
        /// It is suggested to consume real-time data by implementing and adding
        /// a proper <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// In case of COMMAND Subscriptions, the value returned by this
        /// method may be misleading, as in COMMAND mode all the keys received, being
        /// part of the same item, will overwrite each other; for COMMAND Subscriptions,
        /// use <seealso cref="Subscription.getCommandValue(string, string, int)"/> instead.<br/>
        /// Note that internal data is cleared when the Subscription is 
        /// unsubscribed from.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time; if called 
        /// to retrieve a value that has not been received yet, then it will return null.
        /// </summary>
        /// <param name="itemName"> an item in the configured "Item List" </param>
        /// <param name="fieldPos"> the 1-based position of a field within the configured "Field Schema"
        /// or "Field List" </param>
        /// <returns> the current value for the specified field of the specified item
        /// (possibly null), or null if no value has been received yet.
        /// </returns>
        public virtual string getValue(string itemName, int fieldPos)
        {
            lock (this)
            {
                this.verifyFieldPos(fieldPos, false);
                return this.oldValuesByItem.get(this.toItemPos(itemName).Value, fieldPos);
            }
        }

        /// <summary>
        /// Returns the latest value received for the specified item/field pair.<br/>
        /// It is suggested to consume real-time data by implementing and adding
        /// a proper <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// In case of COMMAND Subscriptions, the value returned by this
        /// method may be misleading, as in COMMAND mode all the keys received, being
        /// part of the same item, will overwrite each other; for COMMAND Subscriptions,
        /// use <seealso cref="Subscription.getCommandValue(int, string, string)"/> instead.<br/>
        /// Note that internal data is cleared when the Subscription is 
        /// unsubscribed from.<br/>
        /// <br/>
        /// <b>Lifecycle:</b>  This method can be called at any time; if called 
        /// to retrieve a value that has not been received yet, then it will return null. </summary>
        /// <param name="itemPos"> the 1-based position of an item within the configured "Item Group"
        /// or "Item List" </param>
        /// <param name="fieldName"> a field in the configured "Field List" </param>
        /// <returns> the current value for the specified field of the specified item
        /// (possibly null), or null if no value has been received yet.
        /// </returns>
        public virtual string getValue(int itemPos, string fieldName)
        {
            lock (this)
            {
                this.verifyItemPos(itemPos);
                return this.oldValuesByItem.get(itemPos, this.toFieldPos(fieldName).Value);
            }
        }
        /// <summary>
        /// Returns the latest value received for the specified item/key/field combination. 
        /// This method can only be used if the Subscription mode is COMMAND. 
        /// Subscriptions with two-level behavior
        /// are also supported, hence the specified field 
        /// (see <seealso cref="Subscription.CommandSecondLevelFields"/> and <seealso cref="Subscription.CommandSecondLevelFieldSchema"/>)
        /// can be either a first-level or a second-level one. <br/>
        /// It is suggested to consume real-time data by implementing and adding a proper 
        /// <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// Note that internal data is cleared when the Subscription is unsubscribed from.
        /// </summary>
        /// <param name="itemName"> an item in the configured "Item List" </param>
        /// <param name="keyValue"> the value of a key received on the COMMAND subscription. </param>
        /// <param name="fieldName"> a field in the configured "Field List" </param>
        /// <returns> the current value for the specified field of the specified key within the 
        /// specified item (possibly null), or null if the specified key has not been added yet 
        /// (note that it might have been added and then deleted). </returns>
        public virtual string getCommandValue(string itemName, string keyValue, string fieldName)
        {
            lock (this)
            {
                this.commandCheck();

                string mapKey = this.toItemPos(itemName) + " " + keyValue;
                return this.oldValuesByKey.get(mapKey, this.toFieldPos(fieldName).Value);
            }
        }

        /// <summary>
        /// Returns the latest value received for the specified item/key/field combination. 
        /// This method can only be used if the Subscription mode is COMMAND. 
        /// Subscriptions with two-level behavior
        /// (see <seealso cref="Subscription.CommandSecondLevelFields"/> and <seealso cref="Subscription.CommandSecondLevelFieldSchema"/>)
        /// are also supported, hence the specified field 
        /// can be either a first-level or a second-level one. <br/>
        /// It is suggested to consume real-time data by implementing and adding a proper 
        /// <seealso cref="SubscriptionListener"/> rather than probing this method. <br/>
        /// Note that internal data is cleared when the Subscription is unsubscribed from.
        /// </summary>
        /// <param name="itemPos"> the 1-based position of an item within the configured "Item Group"
        /// or "Item List" </param>
        /// <param name="keyValue"> the value of a key received on the COMMAND subscription. </param>
        /// <param name="fieldPos"> the 1-based position of a field within the configured "Field Schema"
        /// or "Field List" </param>
        /// <returns> the current value for the specified field of the specified key within the 
        /// specified item (possibly null), or null if the specified key has not been added yet 
        /// (note that it might have been added and then deleted). </returns>
        public virtual string getCommandValue(int itemPos, string keyValue, int fieldPos)
        {
            lock (this)
            {
                this.commandCheck();

                this.verifyItemPos(itemPos);
                this.verifyFieldPos(fieldPos, true);

                string mapKey = itemPos + " " + keyValue;
                return this.oldValuesByKey.get(mapKey, fieldPos);

            }
        }

        /// <summary>
        /// Returns the latest value received for the specified item/key/field combination. 
        /// This method can only be used if the Subscription mode is COMMAND. 
        /// Subscriptions with two-level behavior
        /// (see <seealso cref="Subscription.CommandSecondLevelFields"/> and <seealso cref="Subscription.CommandSecondLevelFieldSchema"/>)
        /// are also supported, hence the specified field 
        /// can be either a first-level or a second-level one.<br/>
        /// It is suggested to consume real-time data by implementing and adding a proper 
        /// <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// Note that internal data is cleared when the Subscription is unsubscribed from.
        /// </summary>
        /// <param name="itemPos"> the 1-based position of an item within the configured "Item Group"
        /// or "Item List" </param>
        /// <param name="keyValue"> the value of a key received on the COMMAND subscription. </param>
        /// <param name="fieldName"> a field in the configured "Field List" </param>
        /// <returns> the current value for the specified field of the specified key within the 
        /// specified item (possibly null), or null if the specified key has not been added yet 
        /// (note that it might have been added and then deleted). </returns>
        public virtual string getCommandValue(int itemPos, string keyValue, string fieldName)
        {
            lock (this)
            {
                this.commandCheck();

                this.verifyItemPos(itemPos);

                string mapKey = itemPos + " " + keyValue;
                return this.oldValuesByKey.get(mapKey, this.toFieldPos(fieldName).Value);
            }
        }

        /// <summary>
        /// Returns the latest value received for the specified item/key/field combination. 
        /// This method can only be used if the Subscription mode is COMMAND. 
        /// Subscriptions with two-level behavior
        /// (see <seealso cref="Subscription.CommandSecondLevelFields"/> and <seealso cref="Subscription.CommandSecondLevelFieldSchema"/>)
        /// are also supported, hence the specified field 
        /// can be either a first-level or a second-level one.<br/>
        /// It is suggested to consume real-time data by implementing and adding a proper 
        /// <seealso cref="SubscriptionListener"/> rather than probing this method.<br/>
        /// Note that internal data is cleared when the Subscription is unsubscribed from.
        /// </summary>
        /// <param name="itemName"> an item in the configured "Item List" </param>
        /// <param name="keyValue"> the value of a key received on the COMMAND subscription. </param>
        /// <param name="fieldPos"> the 1-based position of a field within the configured "Field Schema"
        /// or "Field List" </param>
        /// <returns> the current value for the specified field of the specified key within the 
        /// specified item (possibly null), or null if the specified key has not been added yet 
        /// (note that it might have been added and then deleted). </returns>
        public virtual string getCommandValue(string itemName, string keyValue, int fieldPos)
        {
            lock (this)
            {
                this.commandCheck();

                this.verifyFieldPos(fieldPos, true);

                string mapKey = this.toItemPos(itemName) + " " + keyValue;
                return this.oldValuesByKey.get(mapKey, fieldPos);
            }
        }

        //////////////////////////Lifecycle  

        internal virtual void notAliveCheck()
        {
            if (this.Active)
            {
                throw new System.InvalidOperationException(IS_ALIVE);
            }
        }

        internal virtual void isAliveCheck()
        {
            if (!this.Active)
            {
                throw new System.InvalidOperationException(NOT_ALIVE);
            }
        }

        internal virtual void setActive()
        {
            this.notAliveCheck();
            if (this.itemDescriptor == null)
            {
                throw new System.ArgumentException(NO_ITEMS);
            }
            if (this.fieldDescriptor == null)
            {
                throw new System.ArgumentException(NO_FIELDS);
            }

            this.isActive = true;
        }

        internal virtual void setInactive()
        {
            this.isAliveCheck();
            this.isActive = false;
        }

        internal virtual int SubscriptionId
        {
            get
            {
                return this.subscriptionId;
            }
        }

        private bool @is(string what)
        {
            return this.tablePhaseType.Equals(what);
        }

        private bool isNot(string what)
        {
            return !this.@is(what);
        }

        private void setPhase(string what)
        {
            this.tablePhaseType = what;
            this.tablePhase++;
        }

        internal virtual int getPhase()
        {
            return this.tablePhase;
        }

        internal virtual bool checkPhase(int phase)
        {
            return phase == this.tablePhase;
        }

        internal virtual void onAdd(int subId, SubscriptionManager manager, SessionThread sessionThread)
        {
            if (this.isNot(OFF))
            {
                log.Error("Add event already executed");
            }
            this.sessionThread = sessionThread;
            this.subscriptionId = subId;
            this.manager = manager;
            this.setPhase(WAITING);
            this.snapshotManager = new SnapshotManager(isRequiredSnapshot, mode);

            if (log.IsDebugEnabled)
            {
                log.Debug("Subscription " + subId + " ready to be sent to server");
            }
        }

        internal virtual void onStart()
        {
            if (this.isNot(PAUSED))
            {
                log.Error("Unexpected start while not paused");
            }
            this.setPhase(WAITING);

            if (log.IsDebugEnabled)
            {
                log.Debug("Subscription " + this.subscriptionId + " ready to be sent to server");
            }
        }

        internal virtual void onRemove()
        {
            bool wasSubscribed = this.@is(PUSHING);

            log.Debug("set OFF sub on Remove.");

            this.setPhase(OFF);

            if (wasSubscribed)
            {
                this.dispatcher.dispatchEvent(new SubscriptionListenerUnsubscriptionEvent());
            }

            if (this.behavior.Equals(MULTIMETAPUSH))
            {
                this.removeSubTables();
            }
            this.cleanData();

            if (log.IsDebugEnabled)
            {
                log.Debug("Subscription " + this.subscriptionId + " is now off");
            }

        }

        internal virtual void onPause()
        {
            if (this.@is(OFF))
            {
                log.Error("Unexpected pause");
            }


            log.Debug("Set PAUSED sub on Pause.");

            bool wasSubscribed = this.@is(PUSHING);
            this.setPhase(PAUSED);

            if (wasSubscribed)
            {
                this.dispatcher.dispatchEvent(new SubscriptionListenerUnsubscriptionEvent());
            }
            if (this.behavior.Equals(MULTIMETAPUSH))
            {
                this.removeSubTables();
            }
            this.cleanData();

            if (log.IsDebugEnabled)
            {
                log.Debug("Subscription " + this.subscriptionId + " is now on hold");
            }

        }

        internal virtual void onSubscriptionSent()
        {
            if (this.@is(SUBSCRIBING))
            {
                //first subscribe failed, try again
                return;
            }
            else if (this.isNot(WAITING))
            {
                log.Error("Was not expecting the subscription request");
            }
            this.setPhase(SUBSCRIBING);

            if (log.IsDebugEnabled)
            {
                log.Debug("Subscription " + this.subscriptionId + " sent to server");
            }
        }

        internal virtual void unsupportedCommandWithFieldSchema()
        {
            //can't handle command stuff if I don't know which is the key and which is the command

            this.setPhase(PAUSED);
            this.dispatcher.dispatchEvent(new SubscriptionListenerSubscriptionErrorEvent(23, "current client/server pair does not support COMMAND subscriptions containing field schema: specify a field list"));

            this.manager.remove(this);
        }

        internal virtual void onSubscriptionAck()
        {
            //      if (this.isNot(SUBSCRIBING)) {
            //          log.error("Was not expecting the subscribed event");
            //      }

            /* this method was extracted from onSubscribed() to stop the retransmissions when a REQOK is received */
            this.setPhase(PUSHING);
        }

        internal virtual void onSubscribed(int commandPos, int keyPos, int items, int fields)
        {
            //    if (this.isNot(SUBSCRIBING)) {
            //      log.error("Was not expecting the subscribed event");
            //    }
            this.setPhase(PUSHING);

            if (this.behavior.Equals(MULTIMETAPUSH))
            {
                this.fieldDescriptor.SubDescriptor = this.subFieldDescriptor;
            }
            if (this.fieldDescriptor is NameDescriptor && !this.behavior.Equals(SIMPLE))
            {

                this.commandCode = commandPos;
                this.keyCode = keyPos;
            }

            this.itemDescriptor.Size = items;
            this.fieldDescriptor.Size = fields;

            this.dispatcher.dispatchEvent(new SubscriptionListenerSubscriptionEvent());

            if (log.IsDebugEnabled)
            {
                log.Debug("Subscription " + this.subscriptionId + " is now pushing");
            }
        }

        internal virtual void onSubscriptionError(int code, string message)
        {
            if (this.isNot(SUBSCRIBING))
            {
                log.Error("Was not expecting the error event");
            }

            this.setPhase(PAUSED);

            this.dispatcher.dispatchEvent(new SubscriptionListenerSubscriptionErrorEvent(code, message));
        }

        internal virtual bool Off
        {
            get
            {
                return this.@is(OFF);
            }
        }

        internal virtual bool Waiting
        {
            get
            {
                return this.@is(WAITING);
            }
        }

        internal virtual bool Paused
        {
            get
            {
                return this.@is(PAUSED);
            }
        }

        //isSubscribed is part of the API

        internal virtual bool Subscribing
        {
            get
            {
                return this.@is(SUBSCRIBING);
            }
        }

        private bool checkStatusForUpdate()
        {
            /*
             * The method returns true when the update sent by the server is acceptable to the client.
             * 
             * An update can reach the client only in these cases:
             * 1) the client deletes a subscription but the server keeps on sending updates because it has not yet received the deletion:
             * the client must ignore the update
             * 2) the connection is in PUSHING state: the client must accept the update.
             */
            if (!this.Active)
            { //NOTE: this blocks!
              //user does not care anymore, skip
                return false; //not active
                              //case should be rare as soon after the setInactive call the subscription is removed 
                              //from the subscriptions collection
            }
            else if (this.isNot(PUSHING))
            {
                return false; //active but not pushing, unexpected case
            }

            return true; //active and pushing
        }

        //////////////////////////data utils

        internal virtual SubscribeRequest generateSubscribeRequest()
        {
            return new SubscribeRequest(this.subscriptionId, this.mode, this.itemDescriptor, this.fieldDescriptor, this.dataAdapter, this.selector, this.isRequiredSnapshot, this.requestedMaxFrequency, this.requestedBufferSize);
        }

        internal virtual ChangeSubscriptionRequest generateFrequencyRequest()
        {
            return new ChangeSubscriptionRequest(this.subscriptionId, this.requestedMaxFrequency, ++this.nextReconfId);
        }

        internal virtual ChangeSubscriptionRequest generateFrequencyRequest(int reconfId)
        {
            return new ChangeSubscriptionRequest(this.subscriptionId, this.requestedMaxFrequency, reconfId);
        }

        private void prepareSecondLevel()
        {
            if (this.subFieldDescriptor == null)
            {
                //disable second level
                this.behavior = METAPUSH;

            }
            else
            {
                //enable second level
                this.behavior = MULTIMETAPUSH;
            }
        }

        private void secondLevelCheck()
        {
            if (!this.mode.Equals(Constants.COMMAND))
            {
                throw new System.InvalidOperationException(NO_SECOND_LEVEL);
            }
        }

        private void commandCheck()
        {
            if (!this.mode.Equals(Constants.COMMAND))
            {
                throw new System.InvalidOperationException(NO_COMMAND);
            }
        }

        private Descriptor SchemaReadMetapushFields
        {
            set
            {
                if (!this.mode.Equals(Constants.COMMAND) || value == null || value is NameDescriptor)
                {
                    return;
                }

                this.commandCode = value.getPos("command");
                this.keyCode = value.getPos("key");

                if (this.commandCode == -1 || this.keyCode == -1)
                {
                    throw new System.ArgumentException("A field list for a COMMAND subscription must contain the key and command fields");
                }
            }
        }

        ///////////////////////////push events

        internal virtual void endOfSnapshot(int item)
        {
            if (!this.checkStatusForUpdate())
            {
                return;
            }

            string name = this.itemDescriptor.getName(item);
            snapshotManager.endOfSnapshot();
            this.dispatcher.dispatchEvent(new SubscriptionListenerEndOfSnapshotEvent(name, item));
        }

        internal virtual void clearSnapshot(int item)
        {
            if (!this.checkStatusForUpdate())
            {
                return;
            }

            string name = this.itemDescriptor.getName(item);

            if (this.behavior.Equals(METAPUSH))
            {
                //delete key-status
                this.oldValuesByKey.clear();
            }
            else if (this.behavior.Equals(MULTIMETAPUSH))
            {
                //delete key-status
                this.oldValuesByKey.clear();
                //unsubscribe subtables
                this.removeItemSubTables(item);
                this.onLocalFrequencyChanged();
            }

            this.dispatcher.dispatchEvent(new SubscriptionListenerClearSnapshotEvent(name, item));
        }

        internal virtual void lostUpdates(int item, int lostUpdates)
        {
            if (!this.checkStatusForUpdate())
            {
                return;
            }
            string name = this.itemDescriptor.getName(item);
            this.dispatcher.dispatchEvent(new SubscriptionListenerItemLostUpdatesEvent(name, item, lostUpdates));
        }

        internal virtual void configure(string frequency)
        {
            if (!this.checkStatusForUpdate())
            {
                return;
            }
            if (frequency.Equals("unlimited", StringComparison.OrdinalIgnoreCase))
            {
                this.localRealMaxFrequency = FREQUENCY_UNLIMITED;
            }
            else
            {
                try
                {
                    this.localRealMaxFrequency = double.Parse(frequency, CultureInfo.InvariantCulture);
                }
                catch (System.FormatException)
                {
                    // assert(false); // should have been checked by the caller
                    // too late to handle the error, just ignore the information
                    log.Error("Invalid frequency received from the Server for subscription " + this.subscriptionId + ": ignored");
                    this.localRealMaxFrequency = FREQUENCY_NULL;
                }
            }
            if (behavior.Equals(MULTIMETAPUSH))
            {
                this.onLocalFrequencyChanged();
                // may invoke dispatchEvent
            }
            else
            {
                this.dispatcher.dispatchEvent(new SubscriptionListenerConfigurationEvent(frequency));
                // if it is a second-level subscription, the listener will take care
                // of calling onLocalFrequencyChanged() on the first level
            }
        }

        internal virtual void onLostUpdates(string relKey, int lostUpdates)
        {
            if (!this.checkStatusForUpdate())
            {
                return;
            }
            this.dispatcher.dispatchEvent(new SubscriptionListenerCommandSecondLevelItemLostUpdatesEvent(lostUpdates, relKey));
        }

        internal virtual void onServerError(int code, string message, string relKey)
        {
            if (!this.checkStatusForUpdate())
            {
                return;
            }
            this.dispatcher.dispatchEvent(new SubscriptionListenerCommandSecondLevelSubscriptionErrorEvent(code, message, relKey));
        }

        internal virtual void update(List<string> args, int item, bool fromMultison)
        {
            log.Debug("Subscription update - " + item + ", " + fromMultison + ", " + args.Count + ", " + this.checkStatusForUpdate());

            if (!this.checkStatusForUpdate())
            {
                return;
            }

            snapshotManager.update();

            SortedSet<int> changedFields = this.prepareChangedSet(args);

            string key = item.ToString();

            if (!this.behavior.Equals(SIMPLE))
            {
                //handle metapush update
                try
                {
                    key = this.organizeMPUpdate(args, item, fromMultison, changedFields);
                }
                catch (Exception e)
                {
                    log.Warn("Somethig went wrong here: " + e.Message + " - " + e.StackTrace);
                }

                //NOTE, args is now modified!

                //args enters UNCHANGED by item exits UNCHANGED by key
                //oldValuesByItem is updated with new values

            }

            if (this.behavior.Equals(MULTIMETAPUSH) && !fromMultison)
            {
                //2 level push
                //here we handle subscription/unsubscription for the second level
                //(obviously we do not have anything to do if the update is from the second level)
                try
                {
                    this.handleMultiTableSubscriptions(item, args);
                }
                catch (Exception e)
                {
                    log.Warn("Somethig went wrong here: " + e.Message + " - " + e.StackTrace);
                }
            }

            if (this.behavior.Equals(SIMPLE))
            {
                this.updateStructure(this.oldValuesByItem, item, args, changedFields);
            }
            else
            {
                this.updateStructure(this.oldValuesByKey, key, args, changedFields);
                //organizeMPUpdate has already updated the oldValuesByItem array
            }

            string itemName = itemDescriptor.getName(item);
            ItemUpdate updateObj = new ItemUpdate(itemName, item, snapshotManager.Snapshot, args, changedFields, fieldDescriptor);

            this.dispatcher.dispatchEvent(new SubscriptionListenerItemUpdateEvent(updateObj));

            if (!this.behavior.Equals(SIMPLE))
            {
                string command = this.oldValuesByKey.get(key, this.commandCode);
                if (Constants.DELETE.Equals(command))
                {
                    this.oldValuesByKey.delRow(key);
                }
            }

        }

        /////////////////data handling  

        private void cleanData()
        {
            //this.subscriptionId = -1;
            //this.manager = null;

            this.oldValuesByItem.clear();
            this.oldValuesByKey.clear();

            //resets the schema size
            this.fieldDescriptor.Size = 0;
            this.itemDescriptor.Size = 0;

            if (this.behavior.Equals(MULTIMETAPUSH))
            {
                this.fieldDescriptor.SubDescriptor = null;
                this.subTables.clear();
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("structures reset for subscription " + this.subscriptionId);
            }
        }

        private SortedSet<int> prepareChangedSet(List<string> args)
        {
            SortedSet<int> changedFields = new SortedSet<int>();
            for (int i = 0; i < args.Count; i++)
            {
                if (!string.ReferenceEquals(ProtocolConstants.UNCHANGED, args[i]))
                {
                    changedFields.Add(i + 1);
                }
            }
            return changedFields;
        }

        private void updateStructure<K>(ConcurrentMatrix<K, int> @struct, K key, List<string> args, SortedSet<int> changedFields)
        {
            try
            {
                for (int i = 0; i < args.Count; i++)
                {
                    int fieldPos = i + 1;
                    string value = args[i];

                    if (!string.ReferenceEquals(ProtocolConstants.UNCHANGED, value))
                    {
                        @struct.insert(value, key, fieldPos);
                    }
                    else
                    {
                        string oldValue = @struct.get(key, fieldPos);
                        args[i] = oldValue;
                    }
                }
            }
            catch (Exception e)
            {
                log.Warn("warn: " + e.Message);
            }
        }

        private string organizeMPUpdate(List<string> args, int item, bool fromMultison, SortedSet<int> changedFields)
        {
            string extendedKey;

            int numFields = args.Count;
            if (this.commandCode > numFields || this.keyCode > numFields)
            {
                log.Error("key and/or command position not correctly configured");
                return null;
            }

            //we still have the server UNCHANGED here, so we need to evaluate the correct value for the key
            string currentKey = args[this.keyCode - 1];
            if (string.ReferenceEquals(ProtocolConstants.UNCHANGED, currentKey))
            {
                //key is unchyanged, get the old value
                extendedKey = item + " " + this.oldValuesByItem.get(item, this.keyCode);
            }
            else
            {

                extendedKey = item + " " + currentKey;
            }

            //replace unchanged by item with unchanged by key and prepare the old
            //by item for the next time
            //makes only sense for COMMAND update, updates from second level are
            //already organized by key

            if (!fromMultison)
            {
                changedFields.Clear();

                for (int i = 0; i < args.Count; i++)
                {
                    string current = args[i];
                    int fieldPos = i + 1;
                    string oldByItem = this.oldValuesByItem.get(item, fieldPos);

                    if (string.ReferenceEquals(ProtocolConstants.UNCHANGED, current))
                    {
                        //unchanged from server, replace with old by item
                        current = oldByItem;
                        args[i] = oldByItem;
                    }
                    else
                    {
                        //changed from server, put it on the old by item
                        this.oldValuesByItem.insert(current, item, fieldPos);
                    }

                    string oldByKey = this.oldValuesByKey.get(extendedKey, fieldPos);
                    if (( string.ReferenceEquals(oldByKey, null) && string.ReferenceEquals(current, null) ) || ( !string.ReferenceEquals(oldByKey, null) && oldByKey.Equals(current) ))
                    {
                        //i.e.: if (oldByKey == current)
                        //  it means that old and new by key are equals, thus the value is UNCHANGED
                        args[i] = ProtocolConstants.UNCHANGED;
                    }
                    else
                    {
                        //or else 
                        changedFields.Add(fieldPos);
                    }

                }

                if (this.behavior.Equals(MULTIMETAPUSH))
                {
                    int newL = this.fieldDescriptor.FullSize;
                    if (newL > args.Count)
                    {
                        //I got an update on the first level, 
                        //fill the second level fields with unchanged values

                        for (int i = args.Count; i < newL; i++)
                        {
                            args.Add(ProtocolConstants.UNCHANGED);
                        }
                    }
                }
            }
            else
            {
                //update from the second level, the update (args) is already long enough for both levels

                //key is not changed for sure
                args[this.keyCode - 1] = ProtocolConstants.UNCHANGED;
                changedFields.Remove(this.keyCode);

                //command is probably not changed 
                string updateCommand = args[this.commandCode - 1];
                string prevCommand = this.oldValuesByKey.get(extendedKey, this.commandCode);
                if (updateCommand.Equals(prevCommand))
                { //NOTE: update can't be null
                    args[this.commandCode - 1] = ProtocolConstants.UNCHANGED;
                    changedFields.Remove(this.commandCode);
                }
                else
                {
                    changedFields.Add(this.commandCode);
                }

                //other 1st level fiedls are already UNCHANGED

            }
            return extendedKey;
        }

        //////////////////second level handling  

        private void handleMultiTableSubscriptions(int item, List<string> args)
        {
            // subscription/unsubscription of second level subscriptions 

            string key = args[this.keyCode - 1];
            if (string.ReferenceEquals(key, ProtocolConstants.UNCHANGED))
            {
                key = this.oldValuesByItem.get(item, this.keyCode);
            }

            string itemCommand = args[this.commandCode - 1];

            bool subTableExists = this.hasSubTable(item, key);
            if (Constants.DELETE.Equals(itemCommand))
            {
                if (subTableExists)
                {
                    this.removeSubTable(item, key, CLEAN);
                    this.onLocalFrequencyChanged();
                }
            }
            else if (!subTableExists)
            {
                this.addSubTable(item, key);
                // this.onLocalFrequencyChanged(); (useless)
            }
        }

        private void onLocalFrequencyChanged()
        {
            Debug.Assert(behavior.Equals(MULTIMETAPUSH));
            Debug.Assert(!SubTable);
            double prevRealMaxFrequency = aggregatedRealMaxFrequency;

            aggregatedRealMaxFrequency = localRealMaxFrequency;
            this.subTables.forEachElement(new ElementCallbackAnonymousInnerClass(this));

            if (aggregatedRealMaxFrequency != prevRealMaxFrequency)
            {
                string frequency;
                if (aggregatedRealMaxFrequency == FREQUENCY_UNLIMITED)
                {
                    frequency = "unlimited";
                }
                else if (aggregatedRealMaxFrequency == FREQUENCY_NULL)
                {
                    frequency = null;
                }
                else
                {
                    frequency = aggregatedRealMaxFrequency.ToString();
                }
                this.dispatcher.dispatchEvent(new SubscriptionListenerConfigurationEvent(frequency));
            }
        }

        private class ElementCallbackAnonymousInnerClass : Matrix<int, string, Subscription>.ElementCallback<int, string, Subscription>
        {
            private readonly Subscription outerInstance;

            public ElementCallbackAnonymousInnerClass(Subscription outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public bool onElement(Subscription value, int? item, string key)
            {
                if (isHigherFrequency(value.localRealMaxFrequency, outerInstance.aggregatedRealMaxFrequency))
                {
                    outerInstance.aggregatedRealMaxFrequency = value.localRealMaxFrequency;
                }
                return false;
            }

            public bool onElement(Subscription value, int row, string col)
            {
                if (isHigherFrequency(value.localRealMaxFrequency, outerInstance.aggregatedRealMaxFrequency))
                {
                    outerInstance.aggregatedRealMaxFrequency = value.localRealMaxFrequency;
                }
                return false;
            }

            private bool isHigherFrequency(double fNew, double fOld)
            {
                if (fOld == FREQUENCY_UNLIMITED || fNew == FREQUENCY_NULL)
                {
                    return false;
                }
                else if (fNew == FREQUENCY_UNLIMITED || fOld == FREQUENCY_NULL)
                {
                    return true;
                }
                else
                {
                    return fNew > fOld;
                }
            }
        }

        // package-protected instead of private to enable special testing
        internal virtual void addSubTable(int item, string key)
        {

            Subscription secondLevelSubscription = new Subscription(this.subMode);
            secondLevelSubscription.makeSubTable();

            try
            {
                secondLevelSubscription.Items = new string[] { key };
                this.subTables.insert(secondLevelSubscription, item, key);
            }
            catch (System.ArgumentException e)
            {
                log.Error("Subscription error", e);
                onServerError(14, INVALID_SECOND_LEVEL_KEY, key);
                return;
            }

            if (this.subFieldDescriptor is ListDescriptor)
            {
                secondLevelSubscription.Fields = ( (ListDescriptor)subFieldDescriptor ).Original;
            }
            else
            {
                secondLevelSubscription.FieldSchema = ( (NameDescriptor)subFieldDescriptor ).Original;
            }

            secondLevelSubscription.DataAdapter = this.underDataAdapter;
            secondLevelSubscription.RequestedSnapshot = "yes";
            secondLevelSubscription.requestedMaxFrequency = this.requestedMaxFrequency;

            SubscriptionListener subListener = new SecondLevelSubscriptionListener(this, item, key);
            secondLevelSubscription.addListener(subListener);

            secondLevelSubscription.setActive();
            this.manager.doAdd(secondLevelSubscription);

        }

        private void makeSubTable()
        {
            this.subTableFlag = true;
        }
        internal virtual bool SubTable
        {
            get
            {
                //do not abuse
                return this.subTableFlag;
            }
        }
        private bool hasSubTable(int item, string key)
        {
            return this.subTables.get(item, key) != null;
        }

        private void removeSubTable(int item, string key, bool clean)
        {
            Subscription secondLevelSubscription = this.subTables.get(item, key);
            secondLevelSubscription.setInactive();
            this.manager.doRemove(secondLevelSubscription);
            if (clean)
            {
                this.subTables.del(item, key);
            }
        }

        private void removeItemSubTables(int item)
        {
            this.subTables.forEachElementInRow(item, new ElementCallbackAnonymousInnerClass2(this, item));
        }

        private class ElementCallbackAnonymousInnerClass2 : Matrix<int, string, Subscription>.ElementCallback<int, string, Subscription>
        {
            private readonly Subscription outerInstance;

            private int item;

            public ElementCallbackAnonymousInnerClass2(Subscription outerInstance, int item)
            {
                this.outerInstance = outerInstance;
                this.item = item;
            }

            public bool onElement(Subscription value, int? item, string key)
            {
                outerInstance.removeSubTable(item.Value, key, DONT_CLEAN);
                return true;
            }

            public bool onElement(Subscription value, int item, string key)
            {
                outerInstance.removeSubTable(item, key, DONT_CLEAN);
                return true;
            }
        }

        private void removeSubTables()
        {
            this.subTables.forEachElement(new ElementCallbackAnonymousInnerClass3(this));
        }

        private class ElementCallbackAnonymousInnerClass3 : Matrix<int, string, Subscription>.ElementCallback<int, string, Subscription>
        {
            private readonly Subscription outerInstance;

            public ElementCallbackAnonymousInnerClass3(Subscription outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public bool onElement(Subscription value, int? item, string key)
            {
                outerInstance.removeSubTable(item.Value, key, DONT_CLEAN);
                return true;
            }

            public bool onElement(Subscription value, int item, string key)
            {
                outerInstance.removeSubTable(item, key, DONT_CLEAN);
                return true;
            }
        }

        private int SecondLevelSchemaSize
        {
            set
            {
                this.subFieldDescriptor.Size = value;
            }
        }

        ////////////////////////helpers
        private void debugDescriptor(string debugString, Descriptor desc)
        {
            if (log.IsDebugEnabled)
            {
                string s = desc != null ? desc.ComposedString : "<null>";
                log.Debug(debugString + s);
            }
        }

        private int FullSchemaSize
        {
            get
            {
                return this.fieldDescriptor.FullSize;
            }
        }

        private int MainSchemaSize
        {
            get
            {
                return this.fieldDescriptor.Size;
            }
        }

        private int? toFieldPos(string fieldName)
        {
            int fieldPos = this.fieldDescriptor.getPos(fieldName);
            if (fieldPos == -1)
            {
                throw new System.ArgumentException("the specified field does not exist");
            }
            return fieldPos;
        }

        private void verifyFieldPos(int fieldPos, bool full)
        {
            if (fieldPos <= 0 || fieldPos > ( full ? this.fieldDescriptor.FullSize : this.fieldDescriptor.Size ))
            {
                throw new System.ArgumentException("the specified field position is out of bounds");
            }
        }

        private void verifyItemPos(int itemPos)
        {
            if (itemPos <= 0 || itemPos > this.itemDescriptor.Size)
            {
                throw new System.ArgumentException("the specified item position is out of bounds");
            }
        }

        private int? toItemPos(string itemName)
        {
            int itemPos = this.itemDescriptor.getPos(itemName);
            if (itemPos == -1)
            {
                throw new System.ArgumentException("the specified item does not exist");
            }
            return itemPos;
        }

        private class SecondLevelSubscriptionListener : SubscriptionListener
        {
            private readonly Subscription outerInstance;

            internal int itemReference;
            internal string relKey;

            public SecondLevelSubscriptionListener(Subscription outerInstance, int item, string key)
            {
                this.outerInstance = outerInstance;
                this.itemReference = item;
                this.relKey = key;
            }

            public virtual void onClearSnapshot(string itemName, int itemPos)
            {
                // not expected, as MERGE mode is implied here
            }

            public virtual void onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
            {
                // can't happen
            }

            public virtual void onCommandSecondLevelSubscriptionError(int code, string message, string key)
            {
                // can't happen
            }

            public virtual void onEndOfSnapshot(string itemName, int itemPos)
            {
                // nothing to do
            }

            public virtual void onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
            {
                if (!this.shouldDispatch())
                {
                    return;
                }

                outerInstance.onLostUpdates(this.relKey, lostUpdates);
            }

            public virtual void onItemUpdate(ItemUpdate itemUpdate)
            {
                if (!this.shouldDispatch())
                {
                    return;
                }


                outerInstance.SecondLevelSchemaSize = itemUpdate.FieldsCount;

                List<string> args = this.convertMultiSonUpdate(itemUpdate);

                //once the update args are converted we pass them to the main table
                outerInstance.update(args, this.itemReference, true);

            }

            public virtual void onListenEnd(Subscription subscription)
            {
                // don't care
            }

            public virtual void onListenStart(Subscription subscription)
            {
                // don't care

            }

            public virtual void onSubscription()
            {
                // nothing to do
            }

            public virtual void onSubscriptionError(int code, string message)
            {
                if (!this.shouldDispatch())
                {
                    return;
                }

                outerInstance.onServerError(code, message, this.relKey);
            }

            public virtual void onUnsubscription()
            {
                // nothing to do
            }

            internal virtual bool shouldDispatch()
            {
                return outerInstance.hasSubTable(this.itemReference, this.relKey);
            }

            internal virtual List<string> convertMultiSonUpdate(ItemUpdate itemUpdate)
            {

                int y = 1;
                int newLen = outerInstance.FullSchemaSize; //the combined length of the schemas
                List<string> newArgs = new List<string>(newLen);
                for (int i = 0; i < newLen; i++)
                {
                    if (i == outerInstance.keyCode - 1)
                    {
                        //item is our key
                        newArgs.Add(this.relKey);
                    }
                    else if (i == outerInstance.commandCode - 1)
                    {
                        //command must be an UPDATE
                        newArgs.Add(Constants.UPDATE);
                    }
                    else if (i < outerInstance.MainSchemaSize)
                    {
                        //other fields from the first level are unchanged
                        newArgs.Add(ProtocolConstants.UNCHANGED);
                    }
                    else
                    {

                        if (itemUpdate.isValueChanged(y))
                        {
                            //changed fields from the second level
                            newArgs.Add(itemUpdate.getValue(y));
                        }
                        else
                        {
                            newArgs.Add(ProtocolConstants.UNCHANGED);
                        }

                        y++;

                    }
                }

                return newArgs;

            }

            public virtual void onRealMaxFrequency(string frequency)
            {
                // the caller has already updated localRealMaxFrequency on the second-level object
                outerInstance.onLocalFrequencyChanged();
                // this invokes the first-level object
            }

        }

        private string toLowerCase(string s)
        {
            return s.ToLower();
        }

        /// <summary>
        /// Detects whether the current update is a snapshot according to the rules in the following table.
        /// <pre>
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        ///                   |                    | r1    | r2       | r3       | r4      | r5      | r6    | r7    | r8    |
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        /// | snapshot requested | false | true     | true     | true    | true    | true  | true  | true  |
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        /// | mode               | -     | DISTINCT | DISTINCT | COMMAND | COMMAND | MERGE | MERGE | RAW   |
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        /// | first update       | -     | -        | -        | -       | -       | false | true  | -     |
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        /// | EOS received       | -     | false    | true     | false   | true    | -     | -     | -     |
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        /// | isSnapshot()       | false | true     | false    | true    | false   | false | true  | error |
        /// +--------------------+-------+----------+----------+---------+---------+-------+-------+-------+
        /// </pre>
        /// </summary>
        private class SnapshotManager
        {

            internal bool firstUpdate = true;
            internal bool eosReceived = false;
            internal SnapshotManagerState state = SnapshotManagerState.NO_UPDATE_RECEIVED;

            internal readonly string _isRequiredSnapshot;
            internal readonly string _mode;

            internal SnapshotManager(string isRequiredSnapshot, string mode)
            {
                this._isRequiredSnapshot = isRequiredSnapshot;
                this._mode = mode;
            }

            /// <summary>
            /// Notifies the manager that a new update is available.
            /// </summary>
            internal virtual void update()
            {
                if (state == SnapshotManagerState.NO_UPDATE_RECEIVED)
                {
                    state = SnapshotManagerState.ONE_UPDATE_RECEIVED;

                }
                else if (state == SnapshotManagerState.ONE_UPDATE_RECEIVED)
                {
                    state = SnapshotManagerState.MORE_THAN_ONE_UPDATE_RECEIVED;
                    firstUpdate = false;
                }
            }

            /// <summary>
            /// Notifies the manager that the message EOS has arrived.
            /// </summary>
            internal virtual void endOfSnapshot()
            {
                eosReceived = true;
            }

            /// <summary>
            /// Returns true if the user has requested the snapshot.
            /// </summary>
            internal virtual bool snapshotRequested()
            {
                return !string.ReferenceEquals(_isRequiredSnapshot, null) && !_isRequiredSnapshot.Equals("no");
            }

            /// <summary>
            /// Returns true if the current update is a snapshot.
            /// </summary>
            internal virtual bool Snapshot
            {
                get
                {
                    if (!snapshotRequested())
                    {
                        // r1
                        return false;
                    }
                    else if (Constants.MERGE.Equals(_mode))
                    {
                        // r6, r7
                        return firstUpdate;
                    }
                    else if (Constants.COMMAND.Equals(_mode) || Constants.DISTINCT.Equals(_mode))
                    {
                        // r2, r3, r4, r5
                        return !eosReceived;
                    }
                    else
                    {
                        // r8
                        // should never happen
                        Debug.Assert(Constants.RAW.Equals(_mode));
                        return false;
                    }
                }
            }
        }

        /// <summary>
        /// Control states of <seealso cref="SnapshotManager"/>.
        /// </summary>
        private enum SnapshotManagerState
        {
            NO_UPDATE_RECEIVED,
            ONE_UPDATE_RECEIVED,
            MORE_THAN_ONE_UPDATE_RECEIVED
        }
    }
}