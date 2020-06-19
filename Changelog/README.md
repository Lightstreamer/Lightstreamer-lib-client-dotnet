# Lightstreamer Changelog - SDK for .NET Standard Clients




## 5.0.5 - <i>Released on 16 Jun 2020</i>

<i>Compatible with Lightstreamer Server since 7.0.1.</i><br/>

Fixed a bug that could cause the application to crash due to an unexpected URI format.


## 5.0.4 - <i>Released on 12 Jun 2020</i>

<i>Compatible with Lightstreamer Server since 7.0.1.</i><br/>

Fixed a bug that in rare cases could cause the application to crash due to a missing catch of an exception.


## 5.0.3 - <i>Released on 3 Jun 2020</i>

<i>Compatible with Lightstreamer Server since 7.0.1.</i><br/>

Fixed a bug due to a possible race condition that could prevent the stream-sense algorithm from completing.


## 5.0.2 - <i>Released on 18 Mar 2020</i>

<i>Compatible with Lightstreamer Server since 7.0.1.</i><br/>

Fixed a potential memory leak in the internal management of Task objects.

Tuned few DotNetty env parameters.


## 5.0.0 - <i>Released on 16 Dec 2019</i>

<i>Compatible with Lightstreamer Server since 7.0.1.</i><br/>

The new .NET client library introduces full support for the Unified Client API model that we have been introducing in all client libraries for some years now (indeed the first "unified" library was JavaScript 6.0).
The big advantage in using the Unified API is that the same consistent interface and behavior are guaranteed across different client platforms.
In other words, the same abstractions and internal mechanisms are provided for very different platforms (Web, Andorid, Java, iOS, ...), while respecting the conventions, styles, and best practice of each platform.

The API is completely revised compared to the previous version; so your existing code should be revisited to suit the new interface.
Also note that in case you use the Lightstreamer server with a license validation based on file, you may need to request and replace your existing license key files.


## 5.0.0-beta - <i>Released on 8 Oct 2019</i>

<i>Compatible with Lightstreamer Server since 7.0.1.</i><br/>
<i>Not compatible with code developed with the previous version.</i>

The new .NET client library introduces full support for the Unified Client API model that we have been introducing in all client libraries for some years now (indeed the first "unified" library was JavaScript 6.0).
The big advantage in using the Unified API is that the same consistent interface and behavior are guaranteed across different client platforms.
In other words, the same abstractions and internal mechanisms are provided for very different platforms (Web, Andorid, Java, iOS, ...), while respecting the conventions, styles, and best practice of each platform.

The API is completely revised compared to the previous version; so your existing code should be revisited to suit the new interface.
Also note that in case you use the Lightstreamer server with a license validation based on file, you may need to request and replace your existing license key files.
