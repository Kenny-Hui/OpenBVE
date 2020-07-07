﻿using System;
using OpenBveApi.Routes;
using OpenBveApi.Trains;
using RouteManager2;
using RouteManager2.Events;

namespace CsvRwRouteParser
{
	internal struct StopRequest
	{
		internal int StationIndex;
		internal int MaxNumberOfCars;
		internal double TrackPosition;
		internal RequestStop Early;
		internal RequestStop OnTime;
		internal RequestStop Late;
		internal bool FullSpeed;

		internal void CreateEvent(double StartingDistance, double EndingDistance, ref TrackElement Element)
		{
			if (TrackPosition >= StartingDistance & TrackPosition < EndingDistance)
			{
				int m = Element.Events.Length;
				Array.Resize(ref Element.Events, m + 1);
				Element.Events[m] = new RequestStopEvent(Plugin.CurrentRoute, StationIndex, MaxNumberOfCars, FullSpeed, OnTime, Early, Late);
			}
		}
	}
}