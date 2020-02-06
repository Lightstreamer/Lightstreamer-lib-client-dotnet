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
using com.lightstreamer.client.requests;
using com.lightstreamer.client.transport;

namespace com.lightstreamer.client.protocol
{

	public class RequestObjects
	{

	  public readonly LightstreamerRequest request;
	  public readonly RequestTutor tutor;
	  public readonly RequestListener listener;

	  public RequestObjects(LightstreamerRequest request, RequestTutor tutor, RequestListener listener)
	  {
		this.request = request;
		this.tutor = tutor;
		this.listener = listener;

	  }

	}
}