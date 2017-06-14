﻿using System;
using OpenBveApi.Colors;

namespace OpenBve
{
	/*
	 * This class holds the definitions for textual messages
	 */
	partial class MessageManager
	{
		/// <summary>Defines a textual message generated by the game</summary>
		internal class GameMessage : Message
		{
			/// <summary>The internal (non-translated) string</summary>
			internal string InternalText;
			/// <summary>The action which triggered this message (route, speed limit etc.)</summary>
			internal Game.MessageDependency Depencency;

			/// <summary>The font used for this message</summary>
			internal Fonts.OpenGlFont Font;

			internal override void AddMessage()
			{
				//HACK: No way of changing this at the minute....
				Font = Fonts.SmallFont;
				QueueForRemoval = false;
				MessageToDisplay = InternalText;
			}

			internal override void Update()
			{
				//If our message timeout is greater than or equal to the current time, queue it for removal
				bool remove = Game.SecondsSinceMidnight >= Timeout;

				switch (Depencency)
				{
					case Game.MessageDependency.RouteLimit:
					{
						double spd = Math.Abs(TrainManager.PlayerTrain.Specs.CurrentAverageSpeed);
						double lim = TrainManager.PlayerTrain.CurrentRouteLimit;
						//Get the speed and limit in km/h
						spd = Math.Round(spd * 3.6);
						lim = Math.Round(lim * 3.6);
						remove = spd <= lim;
						string s = InternalText, t;
						if (Game.SpeedConversionFactor != 0.0)
						{
							spd = Math.Round(spd * Game.SpeedConversionFactor);
							lim = Math.Round(lim * Game.SpeedConversionFactor);
						}
						t = spd.ToString(System.Globalization.CultureInfo.InvariantCulture);
						s = s.Replace("[speed]", t);
						t = lim.ToString(System.Globalization.CultureInfo.InvariantCulture);
						s = s.Replace("[limit]", t);
						s = s.Replace("[unit]", Game.UnitOfSpeed);
						MessageToDisplay = s;
					} break;
					case Game.MessageDependency.SectionLimit:
					{
						double spd = Math.Abs(TrainManager.PlayerTrain.Specs.CurrentAverageSpeed);
						double lim = TrainManager.PlayerTrain.CurrentSectionLimit;
						spd = Math.Round(spd * 3.6);
						lim = Math.Round(lim * 3.6);
						remove = spd <= lim;
						string s = InternalText, t;
						if (Game.SpeedConversionFactor != 0.0)
						{
							spd = Math.Round(spd * Game.SpeedConversionFactor);
							lim = Math.Round(lim * Game.SpeedConversionFactor);
						}
						t = spd.ToString(System.Globalization.CultureInfo.InvariantCulture);
						s = s.Replace("[speed]", t);
						t = lim.ToString(System.Globalization.CultureInfo.InvariantCulture);
						s = s.Replace("[limit]", t);
						s = s.Replace("[unit]", Game.UnitOfSpeed);
						MessageToDisplay = s;
					} break;
					case Game.MessageDependency.Station:
					{
						int j = TrainManager.PlayerTrain.Station;
						if (j >= 0 & TrainManager.PlayerTrain.StationState != TrainManager.TrainStopState.Completed)
						{
							double d = TrainManager.PlayerTrain.StationDepartureTime - Game.SecondsSinceMidnight + 1.0;
							if (d < 0.0) d = 0.0;
							string s = InternalText;
							TimeSpan a = TimeSpan.FromSeconds(d);
							System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.InvariantCulture;
							string t = a.Hours.ToString("00", Culture) + ":" + a.Minutes.ToString("00", Culture) + ":" + a.Seconds.ToString("00", Culture);
							s = s.Replace("[time]", t);
							s = s.Replace("[name]", Game.Stations[j].Name);
							MessageToDisplay = s;
							if (d > 0.0) remove = false;
						}
						else
						{
							//Queue the mesasge for removal if we have completed the station stop for this message
							remove = true;
						}
					} break;
					default:
						MessageToDisplay = InternalText;
						break;
				}
				if (remove)
				{
					if (Timeout == double.PositiveInfinity)
					{
						Timeout = Game.SecondsSinceMidnight - 1.0;
					}
					//Remove the message if it has completely faded out
					//NOTE: The fadeout is done in the renderer itself...
					if (Game.SecondsSinceMidnight >= Timeout & RendererAlpha == 0.0)
					{
						QueueForRemoval = true;
					}
				}
			}
		}

		/// <summary>Defines a textual message to be displayed in-game</summary>
		internal class GeneralMessage : Message
		{
			/// <summary>The message text to be displayed if early</summary>
			internal string MessageEarlyText;
			/// <summary>The message text to be displayed if on-time</summary>
			internal string MessageOnTimeText;
			/// <summary>The message text to be displayed if late</summary>
			internal string MessageLateText;
			/// <summary>Defines the color of the message</summary>
			internal MessageColor MessageEarlyColor;

			internal MessageColor MessageColor;
			/// <summary>Defines the color of the message</summary>
			internal MessageColor MessageLateColor;
			/// <summary>The font used for this message</summary>
			internal Fonts.OpenGlFont Font;

			internal double MessageEarlyTime;

			internal double MessageLateTime;

			/// <summary>Creates a general textual message</summary>
			internal GeneralMessage()
			{
				this.Timeout = double.PositiveInfinity;
				this.TriggerOnce = true;
				this.Direction = MessageDirection.Forwards;
				this.MessageColor = MessageColor.White;
				this.MessageEarlyColor = MessageColor.White;
				this.MessageLateColor = MessageColor.White;
				this.Font = Fonts.SmallFont;
			}

			internal override void AddMessage()
			{
				if (TriggerOnce && Triggered)
				{
					return;
				}
				Triggered = true;
				if (Game.SecondsSinceMidnight <= MessageEarlyTime)
				{
					//We are early
					if (MessageEarlyText == null)
					{
						QueueForRemoval = true;
						return;
					}
					MessageToDisplay = MessageEarlyText;
					Color = MessageEarlyColor;

				}
				else if (Game.SecondsSinceMidnight >= MessageLateTime)
				{
					//Late
					if (MessageLateText == null)
					{
						QueueForRemoval = true;
						return;
					}
					MessageToDisplay = MessageLateText;
					Color = MessageLateColor;

				}
				else
				{
					//On time
					if (MessageOnTimeText == null)
					{
						QueueForRemoval = true;
						return;
					}
					MessageToDisplay = MessageOnTimeText;
					Color = MessageColor;

				}
				if (this.Timeout != double.PositiveInfinity)
				{
					this.Timeout += Game.SecondsSinceMidnight;
				}
				QueueForRemoval = false;
			}

			internal override void Update()
			{
			}
		}



		/// <summary>Defines a marker text (e.g. bridge name) to be displayed in-game</summary>
		internal class MarkerText : Message
		{
			/// <summary>Defines the color of the marker text</summary>
			internal MessageColor TextColor;
			/// <summary>The font used for this message</summary>
			internal Fonts.OpenGlFont Font;

			/// <summary>Creates a marker text</summary>
			/// <param name="text">The text to be displayed</param>
			internal MarkerText(string text)
			{
				this.MessageToDisplay = text;
				this.Timeout = double.PositiveInfinity;
				this.TriggerOnce = false;
				this.Direction = MessageDirection.Both;
				this.TextColor = MessageColor.White;
				this.RendererAlpha = 1.0;
			}

			/// <summary>Creates a marker text</summary>
			/// <param name="text">The text to be displayed</param>
			/// <param name="Color">The color of the text</param>
			internal MarkerText(string text, MessageColor Color)
			{
				this.MessageToDisplay = text;
				this.Timeout = double.PositiveInfinity;
				this.TriggerOnce = false;
				this.Direction = MessageDirection.Both;
				this.TextColor = Color;
				this.RendererAlpha = 1.0;
			}

			internal override void AddMessage()
			{
				QueueForRemoval = false;
				if (TriggerOnce && Triggered)
				{
					return;
				}
				Triggered = true;
			}

			internal override void Update()
			{
			}
		}
	}
}
