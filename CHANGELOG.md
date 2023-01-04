# Lightstreamer Changelog - SDK for .NET Standard Clients

## [5.1.8] (03-01-2023)

*Compatible with Lightstreamer Server since version 7.0.1*

**Improvements**

- Introduced a new logger `lightstreamer.statistics`, with messages at INFO level about the number of updates received per each Item.

- Improved internal handling of errors in order to mitigate the possibility of unhandled exceptions.

## [5.1.7] (28-11-2022)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

-  Fixed a bug that could lead in some particular cases to wrong encoding of recevied messages.

**Improvements**

- Added further detail logs for websocket upgrade

## [5.1.6] (27-10-2022)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

-  Fixed a bug that could lead to misinterpreting of updates received in cases where multiple websocket frames were involved.

## [5.1.5] (18-08-2022)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

-  Fixed a bug preventing the resolution of hostname ip address of the provided proxy.

## [5.1.4] (24-05-2022)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

-  Fixed the bug reported with [issue #2](https://github.com/Lightstreamer/Lightstreamer-lib-client-dotnet/issues/2).

## [5.1.3] (16-05-2022)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

-  Fixed a bug on subsctiptions management, under particular conditions, could have caused the connection attempts to fail.

## [5.1.2] (23-11-2021)

*Compatible with Lightstreamer Server since version 7.0.1*

**Improvements**

- Replaced Dictionary data structure with ConcurrentDictionary in Matrix management

- Added further detail logs for the client messaging handling

## [5.1.1] (10-08-2021)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

- Fixed a bug in the cookies management that could prevent the content of Set-Cookie header received by the server to be automatically reported in subsequent requests for the same URI.

- Fixed a bug on Websocket transport that could have caused the client to trigger the listener ClientListener.onServerError with error code 61.

## [5.1.0] (17-02-2021)

*Compatible with Lightstreamer Server since version 7.0.1*
*Compatible with code developed with the previous version.*
*If running Lightstreamer Server with a license of "file" type a license upgrade could be needed.*

**New Features**

- Added the new method DisconnectFuture to the LigtstreamerClient class, with the purpose to provide a notification when all tasks started by all LightstreamerClient instances have been terminated, because no more activities need to be managed and hence event dispatching is no longer necessary.
Such method is especially useful in those environments which require an appropriate resource management. The method should be used in replacement of disconnect() in all those circumstances where it is indispensable to guarantee a complete shutdown of all user tasks, in order to avoid potential memory leaks and waste resources.
See the docs for further details about the proper usage pattern for this method.

**Improvements**

- Discontinued a notification to the Server of the termination of a HTTP streaming session.
The notification could help the Server to detect closed connections in some cases, but in other cases it could give rise to bursts of new connections.

- Removed the following dependencies:
	- Akka
	- System.Configuration.ConfigurationManager

- Optimized reverse heartbeat in case of HTTP streaming transport.

**Bug Fixes**

- Fixed a bug affecting the ItemUpdate.isSnapshot method. In case of a subscription of multiple items with a single Subscription object, 
the method returned true only for the first snapshot received. After that, the method returned false even when the updates were indeed snapshots.

- Fixed a bug on connection reuse that, under particular conditions, could have caused some connection attempts to fail.

- Fixed a bug that in rare circumstances could lead to an unmanaged exception when processing the unsubscribe request.


## [5.0.5] (16-06-2020)

*Compatible with Lightstreamer Server since version 7.0.1*

**Bug Fixes**

- Fixed a bug that could cause the application to crash due to an unexpected URI format.

## [5.0.4] (12-06-2020)

*Compatible with Lightstreamer Server since version 7.0.1*  

**Bug Fixes**

- Fixed a bug that in rare cases could cause the application to crash due to a missing catch of an exception.

## [5.0.3] (03-06-2020)

*Compatible with Lightstreamer Server since version 7.0.1*  

**Bug Fixes**

- Fixed a bug due to a possible race condition that could prevent the stream-sense algorithm from completing.

## [5.0.2] (18-03-2020)

*Compatible with Lightstreamer Server since version 7.0.1*  

**Improvements**

- Tuned few DotNetty env parameters.

**Bug Fixes**

- Fixed a potential memory leak in the internal management of Task objects.

## [5.0.0] (16-12-2019)

*Compatible with Lightstreamer Server since version 7.0.1*  

**Improvements**

- The new .NET client library introduces full support for the Unified Client API model that we have been introducing in all client libraries for some years now (indeed the first "unified" library was JavaScript 6.0).
The big advantage in using the Unified API is that the same consistent interface and behavior are guaranteed across different client platforms.
In other words, the same abstractions and internal mechanisms are provided for very different platforms (Web, Andorid, Java, iOS, ...), while respecting the conventions, styles, and best practice of each platform.

- The API is completely revised compared to the previous version; so your existing code should be revisited to suit the new interface.
Also note that in case you use the Lightstreamer server with a license validation based on file, you may need to request and replace your existing license key files.

## [5.0.0-beta] (08-10-2019)

*Compatible with Lightstreamer Server since version 7.0.1*  
*Not compatible with code developed with the previous version.*

- First public release.
