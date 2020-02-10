# Lightstreamer .NET Standard Client SDK 

Lightstreamer .NET Standard Client SDK enables any application which supports Microsoft .Net Standard 2.0 to communicate bidirectionally with a **Lightstreamer Server**.
The API allows to subscribe to real-time data pushed by a Lightstreamer server and to send any message to the server.

The library offers automatic recovery from connection failures, automatic selection of the best available transport, and full decoupling of subscription and connection operations.
It is responsible of forwarding the subscriptions to the Server and re-forwarding all the subscriptions whenever the connection is broken and then reopened.

## Quickstart

To connect to a Lightstreamer Server, a [LightstreamerClient](https://lightstreamer.com/api/ls-dotnetstandard-client/latest/api/com.lightstreamer.client.LightstreamerClient.html) object has to be created, configured, and instructed to connect to a specified endpoint.
A minimal version of the code that creates a LightstreamerClient and connects to the Lightstreamer Server on *https://push.lightstreamer.com* will look like this:

```
LightstreamerClient client = new LightstreamerClient("https://push.lightstreamer.com/", "DEMO");
client.connect();
```

In order to receive real-time updates from the Lightstreamer server the client needs to subscribe to specific Items handled by a Data Adapter deployed at the server-side.
This can be accomplished by instantiating an object of type [Subscription](https://lightstreamer.com/api/ls-dotnetstandard-client/latest/api/com.lightstreamer.client.Subscription.html).
For more details about Subscription in Lightstreamer see the section 3.2 of the [Lightstreamer General Concepts](https://www.lightstreamer.com/docs/base/General%20Concepts.pdf)) documentation.
An axample of subsctiption of three Items of the classic Stock-List example is:

```
Subscription s_stocks = new Subscription("MERGE");

s_stocks.Fields = new string[3] { "last_price", "time", "stock_name" };
s_stocks.Items = new string[3] { "item1", "item2", "item16" };
s_stocks.DataAdapter = "QUOTE_ADAPTER";
s_stocks.RequestedMaxFrequency = "3.0";

s_stocks.addListener(new QuoteListener());
            
client.subscribe(s_stocks);
```

Before sending the subscription to the server, usually at least one [SubscriptionListener](https://lightstreamer.com/api/ls-dotnetstandard-client/latest/api/com.lightstreamer.client.SubscriptionListener.html) is attached to the Subscription instance in order to consume the real-time updates.
The following code shows at console the value of changed fields each time a new update is received for the subscription:

```
    class QuoteListener : SubscriptionListener
    {
        void SubscriptionListener.onClearSnapshot(string itemName, int itemPos)
        {
            Console.WriteLine("Clear Snapshot for " + itemName + ".");
        }

        void SubscriptionListener.onCommandSecondLevelItemLostUpdates(int lostUpdates, string key)
        {
            Console.WriteLine("Lost Updates for " + key + " (" + lostUpdates + ").");
        }

        void SubscriptionListener.onCommandSecondLevelSubscriptionError(int code, string message, string key)
        {
            Console.WriteLine("Subscription Error for " + key + ": " + message);
        }

        void SubscriptionListener.onEndOfSnapshot(string itemName, int itemPos)
        {
            Console.WriteLine("End of Snapshot for " + itemName + ".");
        }

        void SubscriptionListener.onItemLostUpdates(string itemName, int itemPos, int lostUpdates)
        {
            Console.WriteLine("Lost Updates for " + itemName + " (" + lostUpdates + ").");
        }

        void SubscriptionListener.onItemUpdate(ItemUpdate itemUpdate)
        {
            Console.WriteLine("New update for " + itemUpdate.ItemName);

            IDictionary<string, string> listc = itemUpdate.ChangedFields;
            foreach (string value in listc.Values)
            {
                Console.WriteLine(" >>>>>>>>>>>>> " + value);
            }
        }

        void SubscriptionListener.onListenEnd(Subscription subscription)
        {
            // throw new System.NotImplementedException();
        }

        void SubscriptionListener.onListenStart(Subscription subscription)
        {
            // throw new System.NotImplementedException();
        }

        void SubscriptionListener.onRealMaxFrequency(string frequency)
        {
            Console.WriteLine("Real frequency: " + frequency + ".");
        }

        void SubscriptionListener.onSubscription()
        {
            Console.WriteLine("Start subscription.");
        }

        void SubscriptionListener.onSubscriptionError(int code, string message)
        {
            Console.WriteLine("Subscription error: " + message);
        }

        void SubscriptionListener.onUnsubscription()
        {
            Console.WriteLine("Stop subscription.");
        }

    }
```

## Building ##

To build the library, follow below steps:

1. Create a new Visual Studio project (we used Visual Studio 2019) for `Class Library (.NET Standard)`
2. Remove auto created class.cs source file
3. Add all the existing resources contained in the `Lightstreamer_DotNet_Client_Unified` folder
4. Add NuGet references for:
	- Akka
	- CookieManager
	- DotNetty.Buffers
	- DotNetty.Codecs.Http
	- DotNetty.Codecs.Mqtt
	- DotNetty.Common
	- DotNetty.Handlers
	- DotNetty.Transport
	- System.Collections.Immutable
	- System.Configuration.ConfigurationManager
	- System.Management

5. Build the project

## Compatibility ##

The library requires Lightstremaer Server 7.0.1 

## Documentation

- [NuGet package](https://www.nuget.org/packages/Lightstreamer.DotNetStandard.Client/)

- [Live demos](https://demos.lightstreamer.com/?p=lightstreamer&t=client&sclientmicrosoft=dotnet&sclientmicrosoft=xamarin)

- [API Reference](https://lightstreamer.com/api/ls-dotnetstandard-client/latest/api/Index.html)

## Support

For questions and support please use the [Official Forum](https://forums.lightstreamer.com/). The issue list of this page is **exclusively** for bug reports and feature requests.

## License

[Apache 2.0](https://opensource.org/licenses/Apache-2.0)
