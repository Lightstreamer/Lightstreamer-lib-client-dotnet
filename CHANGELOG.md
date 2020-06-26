# Lightstreamer Changelog - SDK for .NET Standard Clients

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
