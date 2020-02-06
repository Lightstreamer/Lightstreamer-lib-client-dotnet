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

	/// <summary>
	/// Utility methods to access non-public methods/fields from a package different from the containing package.
	/// The methods/fields must remain non-public because the containing classes are exported as JavaDoc
	/// and the final user mustn't see them.
	/// </summary>
	public class Internals
	{

		public static Descriptor getItemDescriptor(Subscription sub)
		{
			return sub.itemDescriptor;
		}

		public static Descriptor getFieldDescriptor(Subscription sub)
		{
			return sub.fieldDescriptor;
		}

		public static int getRequestedBufferSize(Subscription sub)
		{
			return sub.requestedBufferSize;
		}

		public static double getRequestedMaxFrequency(Subscription sub)
		{
			return sub.requestedMaxFrequency;
		}
	}
}