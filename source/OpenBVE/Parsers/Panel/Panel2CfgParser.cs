using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibRender2.Trains;
using OpenBve.Parsers.Panel;
using OpenBveApi;
using OpenBveApi.Colors;
using OpenBveApi.Math;
using OpenBveApi.Objects;
using OpenBveApi.Textures;
using OpenBveApi.Interface;
using OpenBveApi.FunctionScripting;
using OpenTK.Graphics.ES20;
using Path = OpenBveApi.Path;

namespace OpenBve {
	internal static class Panel2CfgParser {

		// constants
		private static double StackDistance = 0.000001;
		/// <remarks>EyeDistance is required to be 1.0 by UpdateCarSectionElement and by UpdateCameraRestriction, thus cannot be easily changed.</remarks>
		private const double EyeDistance = 1.0;

		/// <summary>Parses a BVE2 / openBVE panel.cfg file</summary>
		/// <param name="PanelFile">The relative path of the panel configuration file from the train</param>
		/// <param name="TrainPath">The on-disk path to the train</param>
		/// <param name="Car">The car to add the panel to</param>
		internal static void ParsePanel2Config(string PanelFile, string TrainPath, TrainManager.Car Car)
		{
			Encoding Encoding = TextEncoding.GetSystemEncodingFromFile(PanelFile);
			//Train name, used for hacks detection
			string trainName = new System.IO.DirectoryInfo(TrainPath).Name.ToUpperInvariant();
			// read lines
			System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.InvariantCulture;
			string FileName = Path.CombineFile(TrainPath, PanelFile);
			string[] Lines = System.IO.File.ReadAllLines(FileName, Encoding);
			for (int i = 0; i < Lines.Length; i++) {
				Lines[i] = Lines[i].Trim(new char[] { });
				int j = Lines[i].IndexOf(';');
				if (j >= 0)
				{
					Lines[i] = Lines[i].Substring(0, j).TrimEnd(new char[] { });
				}
			}
			// initialize
			double PanelResolution = 1024.0;
			double PanelLeft = 0.0, PanelRight = 1024.0;
			double PanelTop = 0.0, PanelBottom = 1024.0;
			Vector2 PanelCenter = new Vector2(0, 512);
			Vector2 PanelOrigin = new Vector2(0, 512);
			string PanelDaytimeImage = null;
			string PanelNighttimeImage = null;
			Color24 PanelTransparentColor = Color24.Blue;
			// parse lines for panel
			for (int i = 0; i < Lines.Length; i++) {
				if (Lines[i].Length > 0) {
					if (Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal)) {
						string Section = Lines[i].Substring(1, Lines[i].Length - 2).Trim(new char[] { });
						switch (Section.ToLowerInvariant()) {
								// panel
							case "this":
								i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
									int j = Lines[i].IndexOf('='); if (j >= 0)
									{
										string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
										string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
										switch (Key.ToLowerInvariant()) {
											case "resolution":
												double pr = 0.0;
												if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out pr)) {
													Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												}
												if (pr > 100)
												{
													PanelResolution = pr;
												}
												else
												{
													//Parsing very low numbers (Probable typos) for the panel resolution causes some very funky graphical bugs
													//Cap the minimum panel resolution at 100px wide (BVE1 panels are 480px wide, so this is probably a safe minimum)
													Interface.AddMessage(MessageType.Error, false, "A panel resolution of less than 100px was given at line " + (i + 1).ToString(Culture) + " in " + FileName);
												}
												break;
											case "left":
												if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out PanelLeft)) {
													Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line" + (i + 1).ToString(Culture) + " in " + FileName);
												} break;
											case "right":
												if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out PanelRight)) {
													Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												}

												if (Interface.CurrentOptions.EnableBveTsHacks)
												{
													switch ((int) PanelRight)
													{
														case 1696:
															if (PanelResolution == 1024 && trainName == "TOQ2000CN1EXP10" || trainName == "TOQ8500CS8EXP10")
															{
																PanelRight = 1024;
															}
															break;
													}
												}
												break;
											case "top":
												if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out PanelTop)) {
													Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												} break;
											case "bottom":
												if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out PanelBottom)) {
													Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												} break;
											case "daytimeimage":
												if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
												if (Path.ContainsInvalidChars(Value)) {
													Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												} else {
													PanelDaytimeImage = Path.CombineFile(TrainPath, Value);
													if (!System.IO.File.Exists(PanelDaytimeImage)) {
														Interface.AddMessage(MessageType.Error, true, "FileName " + PanelDaytimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														PanelDaytimeImage = null;
													}
												}
												break;
											case "nighttimeimage":
												if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
												if (Path.ContainsInvalidChars(Value)) {
													Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												} else {
													PanelNighttimeImage = Path.CombineFile(TrainPath, Value);
													if (!System.IO.File.Exists(PanelNighttimeImage)) {
														Interface.AddMessage(MessageType.Error, true, "FileName " + PanelNighttimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														PanelNighttimeImage = null;
													}
												}
												break;
											case "transparentcolor":
												if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out PanelTransparentColor)) {
													Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
												} break;
											case "center":
												{
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out PanelCenter.X)) {
															Interface.AddMessage(MessageType.Error, false, "X is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out PanelCenter.Y)) {
															Interface.AddMessage(MessageType.Error, false, "Y is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (Interface.CurrentOptions.EnableBveTsHacks)
														{
															switch ((int)PanelCenter.Y)
															{
																case 180:
																	switch (trainName.ToUpperInvariant())
																	{
																		case "LT_C69_77":
																		case "LT_C69_77_V2":
																			// Broken initial zoom
																			PanelCenter.Y = 350;
																			break;
																	}
																	break;
																case 200:
																	switch (trainName.ToUpperInvariant())
																	{
																		case "HM05":
																			// Broken initial zoom
																			PanelCenter.Y = 350;
																			break;
																	}
																	break;
																case 229:
																	if (PanelBottom == 768 && PanelResolution == 1024)
																	{
																		// Martin Finken's BVE4 trams: Broken initial zoom
																		PanelCenter.Y = 350;
																	}
																	break;
																case 255:
																	if (PanelBottom == 1024 && PanelResolution == 1024)
																	{
																		switch (trainName.ToUpperInvariant())
																		{
																			case "PARIS_MF67":
																			case "PARIS_MF88":
																			case "PARIS_MP73":
																			case "PARIS_MP89":
																			case "PARIS_MP89AUTO":
																			case "LT1938":
																			case "LT1973 UNREFURB":
																				// Broken initial zoom
																				PanelCenter.Y = 350;
																				break;
																			case "LT_A60_62":
																			case "LT1972 MKII":
																				// Broken initial zoom and black patch at bottom of panel
																				PanelCenter.Y = 350;
																				PanelBottom = 792;
																				break;
																		}
																	}
																	break;
															}
															
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												}
											case "origin":
												{
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out PanelOrigin.X)) {
															Interface.AddMessage(MessageType.Error, false, "X is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out PanelOrigin.Y)) {
															Interface.AddMessage(MessageType.Error, false, "Y is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (Interface.CurrentOptions.EnableBveTsHacks)
														{
															switch (trainName)
															{
																case "8171BETA":
																	if (PanelResolution == 768 && PanelOrigin.Y == 256)
																	{
																		// 81-71: Bust panel origin means a flying cab....
																		PanelOrigin.Y = 0;
																	}
																	break;
															}
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												}
										}
									} i++;
								} i--; break;
						}
					}
				}
			}
			{ // camera restriction
				double WorldWidth, WorldHeight;
				if (Program.Renderer.Screen.Width >= Program.Renderer.Screen.Height) {
					WorldWidth = 2.0 * Math.Tan(0.5 * Program.Renderer.Camera.HorizontalViewingAngle) * EyeDistance;
					WorldHeight = WorldWidth / Program.Renderer.Screen.AspectRatio;
				} else {
					WorldHeight = 2.0 * Math.Tan(0.5 * Program.Renderer.Camera.VerticalViewingAngle) * EyeDistance / Program.Renderer.Screen.AspectRatio;
					WorldWidth = WorldHeight * Program.Renderer.Screen.AspectRatio;
				}
				double x0 = (PanelLeft - PanelCenter.X) / PanelResolution;
				double x1 = (PanelRight - PanelCenter.X) / PanelResolution;
				double y0 = (PanelCenter.Y - PanelBottom) / PanelResolution * Program.Renderer.Screen.AspectRatio;
				double y1 = (PanelCenter.Y - PanelTop) / PanelResolution * Program.Renderer.Screen.AspectRatio;
				Car.CameraRestriction.BottomLeft = new Vector3(x0 * WorldWidth, y0 * WorldHeight, EyeDistance);
				Car.CameraRestriction.TopRight = new Vector3(x1 * WorldWidth, y1 * WorldHeight, EyeDistance);
				Car.DriverYaw = Math.Atan((PanelCenter.X - PanelOrigin.X) * WorldWidth / PanelResolution);
				Car.DriverPitch = Math.Atan((PanelOrigin.Y - PanelCenter.Y) * WorldWidth / PanelResolution);
			}
			// create panel
			if (PanelDaytimeImage != null) {
				if (!System.IO.File.Exists(PanelDaytimeImage)) {
					Interface.AddMessage(MessageType.Error, true, "The daytime panel bitmap could not be found in " + FileName);
				} else {
					Texture tday;
					Program.Renderer.TextureManager.RegisterTexture(PanelDaytimeImage, new TextureParameters(null, PanelTransparentColor), out tday);
					Texture tnight = null;
					if (PanelNighttimeImage != null) {
						if (!System.IO.File.Exists(PanelNighttimeImage)) {
							Interface.AddMessage(MessageType.Error, true, "The nighttime panel bitmap could not be found in " + FileName);
						} else {
							Program.Renderer.TextureManager.RegisterTexture(PanelNighttimeImage, new TextureParameters(null, PanelTransparentColor), out tnight);
						}
					}
					OpenBVEGame.RunInRenderThread(() =>
					{
						Program.CurrentHost.LoadTexture(tday, OpenGlTextureWrapMode.ClampClamp);
					});
					CreateElement(ref Car.CarSections[0].Groups[0], 0.0, 0.0, tday.Width, tday.Height, new Vector2(0.5, 0.5), 0.0, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, tday, tnight, Color32.White, false);
				}
			}

			int GroupIndex = 0;

			if (Interface.CurrentOptions.Panel2ExtendedMode)
			{
				GroupIndex++;
				Array.Resize(ref Car.CarSections[0].Groups, GroupIndex + 1);
				Car.CarSections[0].Groups[GroupIndex] = new ElementsGroup(ObjectType.Overlay);
			}

			// parse lines for rest
			double invfac = Lines.Length == 0 ? Loading.TrainProgressCurrentWeight : Loading.TrainProgressCurrentWeight / (double)Lines.Length;
			for (int i = 0; i < Lines.Length; i++) {
				Loading.TrainProgress = Loading.TrainProgressCurrentSum + invfac * (double)i;
				if ((i & 7) == 0) {
					System.Threading.Thread.Sleep(1);
					if (Loading.Cancel) return;
				}
				if (Lines[i].Length > 0) {
					if (Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal)) {
						string Section = Lines[i].Substring(1, Lines[i].Length - 2).Trim(new char[] { });
						switch (Section.ToLowerInvariant()) {
								// pilotlamp
							case "pilotlamp":
								{
									string Subject = "true";
									double LocationX = 0.0, LocationY = 0.0;
									string DaytimeImage = null, NighttimeImage = null;
									Color24 TransparentColor = Color24.Blue;
									int Layer = 0;
									i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
											switch (Key.ToLowerInvariant()) {
												case "subject":
													Subject = Value;
													break;
												case "location":
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out LocationX)) {
															Interface.AddMessage(MessageType.Error, false, "Left is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out LocationY)) {
															Interface.AddMessage(MessageType.Error, false, "Top is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "daytimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														DaytimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(DaytimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + DaytimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															DaytimeImage = null;
														}
													}
													break;
												case "nighttimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														NighttimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(NighttimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + NighttimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															NighttimeImage = null;
														}
													}
													break;
												case "transparentcolor":
													if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out TransparentColor)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out Layer)) {
														Interface.AddMessage(MessageType.Error, false, "LayerIndex is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
											}
										} i++;
									} i--;
									if (DaytimeImage == null) {
										Interface.AddMessage(MessageType.Error, false, "DaytimeImage is required to be specified in " + Section + " in " + FileName);
									}
									// create element
									if (DaytimeImage != null) {
										Texture tday;
										Program.Renderer.TextureManager.RegisterTexture(DaytimeImage, new TextureParameters(null, TransparentColor), out tday);
										Texture tnight = null;
										if (NighttimeImage != null) {
											Program.Renderer.TextureManager.RegisterTexture(NighttimeImage, new TextureParameters(null, TransparentColor), out tnight);
										}
										OpenBVEGame.RunInRenderThread(() =>
										{
											Program.CurrentHost.LoadTexture(tday, OpenGlTextureWrapMode.ClampClamp);
										});
										int w = tday.Width;
										int h = tday.Height;
										int j = CreateElement(ref Car.CarSections[0].Groups[GroupIndex], LocationX, LocationY, w, h, new Vector2(0.5, 0.5), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, tday, tnight, Color32.White, false);
										string f = GetStackLanguageFromSubject(Car.baseTrain, Subject, Section + " in " + FileName);
										try
										{
											Car.CarSections[0].Groups[GroupIndex].Elements[j].StateFunction = new FunctionScript(Program.CurrentHost, f + " 1 == --", false);
										}
										catch
										{
											Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
										}
									}
								} break;
								// needle
							case "needle":
								{
									string Subject = "true";
									double LocationX = 0.0, LocationY = 0.0;
									string DaytimeImage = null, NighttimeImage = null;
									Color32 Color = Color32.White;
									Color24 TransparentColor = Color24.Blue;
									double OriginX = -1.0, OriginY = -1.0;
									bool OriginDefined = false;
									double Layer = 0.0, Radius = 0.0;
									double InitialAngle = -2.0943951023932, LastAngle = 2.0943951023932;
									double Minimum = 0.0, Maximum = 1000.0;
									double NaturalFrequency = -1.0, DampingRatio = -1.0;
									bool Backstop = false, Smoothed = false;
									i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
											switch (Key.ToLowerInvariant()) {
												case "subject":
													Subject = Value;
													break;
												case "location":
													{
														int k = Value.IndexOf(',');
														if (k >= 0)
														{
															string a = Value.Substring(0, k).TrimEnd(new char[] { });
															string b = Value.Substring(k + 1).TrimStart(new char[] { });
															if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out LocationX)) {
																Interface.AddMessage(MessageType.Error, false, "CenterX is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															}
															if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out LocationY)) {
																Interface.AddMessage(MessageType.Error, false, "CenterY is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															}
														} else {
															Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} break;
												case "radius":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Radius)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (Radius == 0.0) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is expected to be non-zero in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														Radius = 16.0;
													} break;
												case "daytimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														DaytimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(DaytimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + DaytimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															DaytimeImage = null;
														}
													}
													break;
												case "nighttimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														NighttimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(NighttimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + NighttimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															NighttimeImage = null;
														}
													}
													break;
												case "color":
													if (Value.Length != 0 && !Color32.TryParseHexColor(Value, out Color)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "transparentcolor":
													if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out TransparentColor)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "origin":
													{
														int k = Value.IndexOf(',');
														if (k >= 0)
														{
															string a = Value.Substring(0, k).TrimEnd(new char[] { });
															string b = Value.Substring(k + 1).TrimStart(new char[] { });
															if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out OriginX)) {
																Interface.AddMessage(MessageType.Error, false, "X is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															}
															if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out OriginY)) {
																Interface.AddMessage(MessageType.Error, false, "Y is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
																OriginX = -OriginX;
															}
															OriginDefined = true;
														} else {
															Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} break;
												case "initialangle":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out InitialAngle)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInDegrees is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "lastangle":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out LastAngle)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInDegrees is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "minimum":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Minimum)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "maximum":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Maximum)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "naturalfreq":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out NaturalFrequency)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (NaturalFrequency < 0.0) {
														Interface.AddMessage(MessageType.Error, false, "Value is expected to be non-negative in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														NaturalFrequency = -NaturalFrequency;
													} break;
												case "dampingratio":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out DampingRatio)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (DampingRatio < 0.0) {
														Interface.AddMessage(MessageType.Error, false, "Value is expected to be non-negative in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														DampingRatio = -DampingRatio;
													} break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Layer)) {
														Interface.AddMessage(MessageType.Error, false, "LayerIndex is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "backstop":
													if (Value.Length != 0 && Value.ToLowerInvariant() == "true" || Value == "1")
													{
														Backstop = true;
													}
													break;
												case "smoothed":
													if (Value.Length != 0 && Value.ToLowerInvariant() == "true" || Value == "1")
													{
														Smoothed = true;
													}
													break;
											}
										} i++;
									} i--;
									if (DaytimeImage == null) {
										Interface.AddMessage(MessageType.Error, false, "DaytimeImage is required to be specified in " + Section + " in " + FileName);
									}
									// create element
									if (DaytimeImage != null)
									{
										Texture tday;
										Program.Renderer.TextureManager.RegisterTexture(DaytimeImage,
											new TextureParameters(null, TransparentColor), out tday);
										Texture tnight = null;
										if (NighttimeImage != null)
										{
											Program.Renderer.TextureManager.RegisterTexture(NighttimeImage,
												new TextureParameters(null, TransparentColor), out tnight);
										}
										OpenBVEGame.RunInRenderThread(() =>
										{
											Program.CurrentHost.LoadTexture(tday, OpenGlTextureWrapMode.ClampClamp);
										});
										double w = (double)tday.Width;
										double h = (double)tday.Height;
										if (!OriginDefined) {
											OriginX = 0.5 * w;
											OriginY = 0.5 * h;
										}
										double ox = OriginX / w;
										double oy = OriginY / h;
										double n = Radius == 0.0 | OriginY == 0.0 ? 1.0 : Radius / OriginY;
										double nx = n * w;
										double ny = n * h;
										int j = CreateElement(ref Car.CarSections[0].Groups[GroupIndex], LocationX - ox * nx, LocationY - oy * ny, nx, ny, new Vector2(ox, oy), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, tday, tnight, Color, false);
										Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateZDirection = Vector3.Backward;
										Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateXDirection = Vector3.Right;
										Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateYDirection = Vector3.Cross(Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateZDirection, Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateXDirection);
										string f;
										switch (Subject.ToLowerInvariant()) {
											case "hour":
												f = Smoothed ? "0.000277777777777778 time * 24 mod" : "0.000277777777777778 time * floor";
												break;
											case "min":
												f = Smoothed ? "0.0166666666666667 time * 60 mod" : "0.0166666666666667 time * floor";
												break;
											case "sec":
												f = Smoothed ? "time 60 mod" : "time floor";
												break;
											default:
												f = GetStackLanguageFromSubject(Car.baseTrain, Subject, Section + " in " + FileName);
												break;
										}

										InitialAngle = InitialAngle.ToRadians();
										LastAngle = LastAngle.ToRadians();
										double a0 = (InitialAngle * Maximum - LastAngle * Minimum) / (Maximum - Minimum);
										double a1 = (LastAngle - InitialAngle) / (Maximum - Minimum);
										f += " " + a1.ToString(Culture) + " * " + a0.ToString(Culture) + " +";
										if (NaturalFrequency >= 0.0 & DampingRatio >= 0.0) {
											Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateZDamping = new Damping(NaturalFrequency, DampingRatio);
										}
										try
										{
											Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateZFunction = new FunctionScript(Program.CurrentHost, f, false);
										}
										catch
										{
											Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
										}
										if (Backstop)
										{
											Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateZFunction.Minimum = InitialAngle;
											Car.CarSections[0].Groups[GroupIndex].Elements[j].RotateZFunction.Maximum = LastAngle;
										}
									}
								} break;
							case "lineargauge":
									{
									string Subject = "true";
									int Width = 0;
									Vector2 Direction = new Vector2(1,0);
									double LocationX = 0.0, LocationY = 0.0;
									string DaytimeImage = null, NighttimeImage = null;
									double Minimum = 0.0, Maximum = 0.0;
									Color24 TransparentColor = Color24.Blue;
									int Layer = 0;
									i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
											switch (Key.ToLowerInvariant()) {
												case "subject":
													Subject = Value;
													break;
												case "location":
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out LocationX)) {
															Interface.AddMessage(MessageType.Error, false, "Left is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out LocationY)) {
															Interface.AddMessage(MessageType.Error, false, "Top is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "minimum":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Minimum))
													{
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "maximum":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Maximum))
													{
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "width":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out Width))
													{
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}
													break;
												case "direction":
													{
														string[] s = Value.Split( new char[] { ',' });
														if (s.Length == 2)
														{
															double x, y;
															if (!double.TryParse(s[0], System.Globalization.NumberStyles.Float, Culture, out x))
															{
																Interface.AddMessage(MessageType.Error, false, "X is invalid in LinearGauge Direction at line " + (i + 1).ToString(Culture) + " in file " + FileName);
															}
															else if (!double.TryParse(s[1], System.Globalization.NumberStyles.Float, Culture, out y))
															{
																Interface.AddMessage(MessageType.Error, false, "Y is invalid in  LinearGauge Direction at line " + (i + 1).ToString(Culture) + " in file " + FileName);
															}
															else
															{
																Direction = new Vector2(x, y);
															}
														}
														else
														{
															Interface.AddMessage(MessageType.Error, false, "Exactly 2 arguments are expected in LinearGauge Direction at line " + (i + 1).ToString(Culture) + " in file " + FileName);
														}
													}
													break;
												case "daytimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														DaytimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(DaytimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + DaytimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															DaytimeImage = null;
														}
													}
													break;
												case "nighttimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														NighttimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(NighttimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + NighttimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															NighttimeImage = null;
														}
													}
													break;
												case "transparentcolor":
													if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out TransparentColor)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out Layer)) {
														Interface.AddMessage(MessageType.Error, false, "LayerIndex is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
											}
										} i++;
									} i--;
									if (DaytimeImage == null) {
										Interface.AddMessage(MessageType.Error, false, "DaytimeImage is required to be specified in " + Section + " in " + FileName);
									}
									// create element
									if (DaytimeImage != null) {
										Texture tday;
										Program.Renderer.TextureManager.RegisterTexture(DaytimeImage, new TextureParameters(null, TransparentColor), out tday);
										Texture tnight = null;
										if (NighttimeImage != null) {
											Program.Renderer.TextureManager.RegisterTexture(NighttimeImage, new TextureParameters(null, TransparentColor), out tnight);
										}
										OpenBVEGame.RunInRenderThread(() =>
										{
											Program.CurrentHost.LoadTexture(tday, OpenGlTextureWrapMode.ClampClamp);
										});
										int w = tday.Width;
										int h = tday.Height;
										int j = CreateElement(ref Car.CarSections[0].Groups[GroupIndex], LocationX, LocationY, w, h, new Vector2(0.5, 0.5), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, tday, tnight, Color32.White, false);
										if (Maximum < Minimum)
										{
											Interface.AddMessage(MessageType.Error, false, "Maximum value must be greater than minimum value " + Section + " in " + FileName);
											break;
										}
										string tf = GetInfixFunction(Car.baseTrain, Subject, Minimum, Maximum, Width, tday.Width, Section + " in " + FileName);
										if (tf != String.Empty)
										{
											Car.CarSections[0].Groups[GroupIndex].Elements[j].TextureShiftXDirection = Direction;
											try
											{
												Car.CarSections[0].Groups[GroupIndex].Elements[j].TextureShiftXFunction = new FunctionScript(Program.CurrentHost, tf, false);
											}
											catch
											{
												Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
											}
										}
									}
								} break;
								// digitalnumber
							case "digitalnumber":
								{
									string Subject = "true";
									double LocationX = 0.0, LocationY = 0.0;
									string DaytimeImage = null, NighttimeImage = null;
									Color24 TransparentColor = Color24.Blue;
									double Layer = 0.0; int Interval = 0;
									i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
											switch (Key.ToLowerInvariant()) {
												case "subject":
													Subject = Value;
													break;
												case "location":
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out LocationX)) {
															Interface.AddMessage(MessageType.Error, false, "Left is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out LocationY)) {
															Interface.AddMessage(MessageType.Error, false, "Top is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "daytimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														DaytimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(DaytimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + DaytimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															DaytimeImage = null;
														}
													}
													break;
												case "nighttimeimage":
													if (!System.IO.Path.HasExtension(Value)) Value += ".bmp";
													if (Path.ContainsInvalidChars(Value)) {
														Interface.AddMessage(MessageType.Error, false, "FileName contains illegal characters in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														NighttimeImage = Path.CombineFile(TrainPath, Value);
														if (!System.IO.File.Exists(NighttimeImage)) {
															Interface.AddMessage(MessageType.Error, true, "FileName " + NighttimeImage + " could not be found in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															NighttimeImage = null;
														}
													}
													break;
												case "transparentcolor":
													if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out TransparentColor)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "interval":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out Interval)) {
														Interface.AddMessage(MessageType.Error, false, "Height is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (Interval <= 0) {
														Interface.AddMessage(MessageType.Error, false, "Height is expected to be non-negative in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Layer)) {
														Interface.AddMessage(MessageType.Error, false, "LayerIndex is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
											}
										} i++;
									} i--;
									if (DaytimeImage == null) {
										Interface.AddMessage(MessageType.Error, false, "DaytimeImage is required to be specified in " + Section + " in " + FileName);
									}
									if (Interval <= 0) {
										Interface.AddMessage(MessageType.Error, false, "Interval is required to be specified in " + Section + " in " + FileName);
									}
									// create element
									if (DaytimeImage != null & Interval > 0) {
										int wday, hday;
										Program.CurrentHost.QueryTextureDimensions(DaytimeImage, out wday, out hday);
										if (wday > 0 & hday > 0) {
											int numFrames = hday / Interval;
											if (Interface.CurrentOptions.EnableBveTsHacks)
											{
												/*
												 * With hacks enabled, the final frame does not necessarily need to be
												 * completely within the confines of the texture
												 * e.g. LT_C69_77
												 * https://github.com/leezer3/OpenBVE/issues/247
												 */
												switch (Subject)
												{
													case "power":
														if (Car.baseTrain.Handles.Power.MaximumNotch > numFrames)
														{
															numFrames = Car.baseTrain.Handles.Power.MaximumNotch;
														}
														break;
													case "brake":
														int b = Car.baseTrain.Handles.Brake.MaximumNotch + 2;
														if (Car.baseTrain.Handles.HasHoldBrake)
														{
															b++;
														}
														if (b > numFrames )
														{
															numFrames = b;
														}
														break;
												}
											}
											Texture[] tday = new Texture[numFrames];
											Texture[] tnight;
											for (int k = 0; k < numFrames; k++)
											{
												if ((k + 1) * Interval <= hday)
												{
													Program.Renderer.TextureManager.RegisterTexture(DaytimeImage, new TextureParameters(new TextureClipRegion(0, k * Interval, wday, Interval), TransparentColor), out tday[k]);
												}
												else if (k * Interval >= hday)
												{
													numFrames = k;
													Array.Resize(ref tday, k);
												}
												else
												{
													Program.Renderer.TextureManager.RegisterTexture(DaytimeImage, new TextureParameters(new TextureClipRegion(0, k * Interval, wday, hday - (k * Interval)), TransparentColor), out tday[k]);
												}
											}
											if (NighttimeImage != null) {
												int wnight, hnight;
												Program.CurrentHost.QueryTextureDimensions(NighttimeImage, out wnight, out hnight);
												tnight = new Texture[numFrames];
												for (int k = 0; k < numFrames; k++) {
													if ((k + 1) * Interval <= hnight)
													{
														Program.Renderer.TextureManager.RegisterTexture(NighttimeImage, new TextureParameters(new TextureClipRegion(0, k * Interval, wnight, Interval), TransparentColor), out tnight[k]);
													}
													else if (k * Interval > hnight)
													{
														tnight[k] = null;
													}
													else
													{
														Program.Renderer.TextureManager.RegisterTexture(NighttimeImage, new TextureParameters(new TextureClipRegion(0, k * Interval, wnight, hnight - (k * Interval)), TransparentColor), out tnight[k]);
													}
												}
												
											} else {
												tnight = new Texture[numFrames];
												for (int k = 0; k < numFrames; k++) {
													tnight[k] = null;
												}
											}
											int j = -1;
											for (int k = 0; k < tday.Length; k++) {
												int l = CreateElement(ref Car.CarSections[0].Groups[GroupIndex], LocationX, LocationY, (double)wday, (double)Interval, new Vector2(0.5, 0.5), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, tday[k], tnight[k], Color32.White, k != 0);
												if (k == 0) j = l;
											}
											string f = GetStackLanguageFromSubject(Car.baseTrain, Subject, Section + " in " + FileName);
											try
											{
												Car.CarSections[0].Groups[GroupIndex].Elements[j].StateFunction = new FunctionScript(Program.CurrentHost, f, false);
											}
											catch
											{
												Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
											}

											if (Interface.CurrentOptions.Panel2ExtendedMode)
											{
												if (wday >= Interface.CurrentOptions.Panel2ExtendedMinSize && Interval >= Interface.CurrentOptions.Panel2ExtendedMinSize)
												{
													if (Subject == "power")
													{
														PanelXmlParser.CreateTouchElement(Car.CarSections[0].Groups[GroupIndex], new Vector2(LocationX, LocationY), new Vector2(wday, Interval / 2.0), GroupIndex - 1, new int[0], new[] { new TrainManager.CommandEntry { Command = Translations.Command.PowerDecrease } }, new Vector2(0.5, 0.5), 0, PanelResolution, PanelBottom, PanelCenter, Car.Driver);
														PanelXmlParser.CreateTouchElement(Car.CarSections[0].Groups[GroupIndex], new Vector2(LocationX, LocationY + Interval / 2.0), new Vector2(wday, Interval / 2.0), GroupIndex - 1, new int[0], new[] { new TrainManager.CommandEntry { Command = Translations.Command.PowerIncrease } }, new Vector2(0.5, 0.5), 0, PanelResolution, PanelBottom, PanelCenter, Car.Driver);
													}

													if (Subject == "brake")
													{
														PanelXmlParser.CreateTouchElement(Car.CarSections[0].Groups[GroupIndex], new Vector2(LocationX, LocationY), new Vector2(wday, Interval / 2.0), GroupIndex - 1, new int[0], new[] { new TrainManager.CommandEntry { Command = Translations.Command.BrakeIncrease } }, new Vector2(0.5, 0.5), 0, PanelResolution, PanelBottom, PanelCenter, Car.Driver);
														PanelXmlParser.CreateTouchElement(Car.CarSections[0].Groups[GroupIndex], new Vector2(LocationX, LocationY + Interval / 2.0), new Vector2(wday, Interval / 2.0), GroupIndex - 1, new int[0], new[] { new TrainManager.CommandEntry { Command = Translations.Command.BrakeDecrease } }, new Vector2(0.5, 0.5), 0, PanelResolution, PanelBottom, PanelCenter, Car.Driver);
													}

													if (Subject == "reverser")
													{
														PanelXmlParser.CreateTouchElement(Car.CarSections[0].Groups[GroupIndex], new Vector2(LocationX, LocationY), new Vector2(wday, Interval / 2.0), GroupIndex - 1, new int[0], new[] { new TrainManager.CommandEntry { Command = Translations.Command.ReverserForward } }, new Vector2(0.5, 0.5), 0, PanelResolution, PanelBottom, PanelCenter, Car.Driver);
														PanelXmlParser.CreateTouchElement(Car.CarSections[0].Groups[GroupIndex], new Vector2(LocationX, LocationY + Interval / 2.0), new Vector2(wday, Interval / 2.0), GroupIndex - 1, new int[0], new[] { new TrainManager.CommandEntry { Command = Translations.Command.ReverserBackward } }, new Vector2(0.5, 0.5), 0, PanelResolution, PanelBottom, PanelCenter, Car.Driver);
													}
												}
											}
										}
									}
								} break;
								// digitalgauge
							case "digitalgauge":
								{
									string Subject = "true";
									double LocationX = 0.0, LocationY = 0.0;
									Color32 Color = Color32.Black;
									double Radius = 0.0;
									int Layer = 0;
									double InitialAngle = -2.0943951023932, LastAngle = 2.0943951023932;
									double Minimum = 0.0, Maximum = 1000.0;
									double Step = 0.0;
									i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
											switch (Key.ToLowerInvariant()) {
												case "subject":
													Subject = Value;
													break;
												case "location":
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out LocationX)) {
															Interface.AddMessage(MessageType.Error, false, "CenterX is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out LocationY)) {
															Interface.AddMessage(MessageType.Error, false, "CenterY is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "radius":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Radius)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (Radius == 0.0) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is expected to be non-zero in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														Radius = 16.0;
													} break;
												case "color":
													if (Value.Length != 0 && !Color32.TryParseHexColor(Value, out Color)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "initialangle":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out InitialAngle)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInDegrees is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														InitialAngle = InitialAngle.ToRadians();
													} break;
												case "lastangle":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out LastAngle)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInDegrees is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else {
														LastAngle = LastAngle.ToRadians();
													} break;
												case "minimum":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Minimum)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "maximum":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Maximum)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "step":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Step)) {
														Interface.AddMessage(MessageType.Error, false, "Value is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out Layer)) {
														Interface.AddMessage(MessageType.Error, false, "LayerIndex is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
											}
										} i++;
									} i--;

									if (Interface.CurrentOptions.EnableBveTsHacks && trainName == "BOEING-737")
									{
										/*
										 * BVE4 stacks objects within layers in order
										 * If two overlapping objects are declared in the same
										 * layer in openBVE, this causes Z-fighting
										 *
										 */
										if (Subject == "sap" || Subject == "bp")
										{
											Layer = 4;
										}
									}

									if (Radius == 0.0) {
										Interface.AddMessage(MessageType.Error, false, "Radius is required to be non-zero in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
									}
									if (Minimum == Maximum) {
										Interface.AddMessage(MessageType.Error, false, "Minimum and Maximum must not be equal in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
										Radius = 0.0;
									}
									if (Math.Abs(InitialAngle - LastAngle) > 6.28318531) {
										Interface.AddMessage(MessageType.Warning, false, "The absolute difference between InitialAngle and LastAngle exceeds 360 degrees in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
									}
									if (Radius != 0.0) {
										// create element
										int j = CreateElement(ref Car.CarSections[0].Groups[GroupIndex], LocationX - Radius, LocationY - Radius, 2.0 * Radius, 2.0 * Radius, new Vector2(0.5, 0.5), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, null, null, Color, false);
										InitialAngle = InitialAngle + Math.PI;
										LastAngle = LastAngle + Math.PI;
										double x0 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[0].Coordinates.X;
										double y0 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[0].Coordinates.Y;
										double z0 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[0].Coordinates.Z;
										double x1 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[1].Coordinates.X;
										double y1 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[1].Coordinates.Y;
										double z1 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[1].Coordinates.Z;
										double x2 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[2].Coordinates.X;
										double y2 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[2].Coordinates.Y;
										double z2 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[2].Coordinates.Z;
										double x3 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[3].Coordinates.X;
										double y3 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[3].Coordinates.Y;
										double z3 = Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh.Vertices[3].Coordinates.Z;
										double cx = 0.25 * (x0 + x1 + x2 + x3);
										double cy = 0.25 * (y0 + y1 + y2 + y3);
										double cz = 0.25 * (z0 + z1 + z2 + z3);
										VertexTemplate[] vertices = new VertexTemplate[11];
										for (int v = 0; v < 11; v++)
										{
											vertices[v] = new Vertex();
										}
										int[][] faces = new int[][] {
											new int[] { 0, 1, 2 },
											new int[] { 0, 3, 4 },
											new int[] { 0, 5, 6 },
											new int[] { 0, 7, 8 },
											new int[] { 0, 9, 10 }
										};
										Car.CarSections[0].Groups[GroupIndex].Elements[j].States[0].Prototype.Mesh = new Mesh(vertices, faces, Color);
										Car.CarSections[0].Groups[GroupIndex].Elements[j].LEDClockwiseWinding = InitialAngle <= LastAngle;
										Car.CarSections[0].Groups[GroupIndex].Elements[j].LEDInitialAngle = InitialAngle;
										Car.CarSections[0].Groups[GroupIndex].Elements[j].LEDLastAngle = LastAngle;
										Car.CarSections[0].Groups[GroupIndex].Elements[j].LEDVectors = new Vector3[] {
											new Vector3(x0, y0, z0),
											new Vector3(x1, y1, z1),
											new Vector3(x2, y2, z2),
											new Vector3(x3, y3, z3),
											new Vector3(cx, cy, cz)
										};
										string f = GetStackLanguageFromSubject(Car.baseTrain, Subject, Section + " in " + FileName);
										double a0 = (InitialAngle * Maximum - LastAngle * Minimum) / (Maximum - Minimum);
										double a1 = (LastAngle - InitialAngle) / (Maximum - Minimum);
										if (Step == 1.0) {
											f += " floor";
										} else if (Step != 0.0) {
											string s = (1.0 / Step).ToString(Culture);
											string t = Step.ToString(Culture);
											f += " " + s + " * floor " + t + " *";
										}
										f += " " + a1.ToString(Culture) + " " + a0.ToString(Culture) + " fma";
										try
										{
											Car.CarSections[0].Groups[GroupIndex].Elements[j].LEDFunction = new FunctionScript(Program.CurrentHost, f, false);
										}
										catch
										{
											Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
										}
									} else {
										Interface.AddMessage(MessageType.Error, false, "Radius is required to be specified in " + Section + " in " + FileName);
									}
								} break;
								// timetable
							case "timetable":
								{
									double LocationX = 0.0, LocationY = 0.0;
									double Width = 0.0, Height = 0.0;
									//We read the transparent color for the timetable from the config file, but it is never used
									//TODO: Fix or depreciate??
									Color24 TransparentColor = Color24.Blue;
									double Layer = 0.0;
									i++; while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal))) {
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });
											switch (Key.ToLowerInvariant()) {
												case "location":
													int k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out LocationX)) {
															Interface.AddMessage(MessageType.Error, false, "X is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out LocationY)) {
															Interface.AddMessage(MessageType.Error, false, "Y is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													} else {
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "width":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Width)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (Width <= 0.0) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is required to be positive in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "height":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Height)) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} else if (Height <= 0.0) {
														Interface.AddMessage(MessageType.Error, false, "ValueInPixels is required to be positive in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "transparentcolor":
													if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out TransparentColor)) {
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out Layer)) {
														Interface.AddMessage(MessageType.Error, false, "LayerIndex is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													} break;
											}
										} i++;
									} i--;
									// create element
									if (Width <= 0.0) {
										Interface.AddMessage(MessageType.Error, false, "Width is required to be specified in " + Section + " in " + FileName);
									}
									if (Height <= 0.0) {
										Interface.AddMessage(MessageType.Error, false, "Height is required to be specified in " + Section + " in " + FileName);
									}
									if (Width > 0.0 & Height > 0.0) {
										int j = CreateElement(ref Car.CarSections[0].Groups[GroupIndex], LocationX, LocationY, Width, Height, new Vector2(0.5, 0.5), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Car.Driver, null, null, Color32.White, false);
										try
										{
											Car.CarSections[0].Groups[GroupIndex].Elements[j].StateFunction = new FunctionScript(Program.CurrentHost, "panel2timetable", false);
										}
										catch
										{
											Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
										}
										
										Timetable.AddObjectForCustomTimetable(Car.CarSections[0].Groups[GroupIndex].Elements[j]);
									}
								} break;
								case "windscreen":
								{
									i++;
									Vector2 topLeft = new Vector2(PanelLeft, PanelTop);
									Vector2 bottomRight = new Vector2(PanelRight, PanelBottom);
									int numberOfDrops = 16, Layer = 0, dropSize = 16;
									WiperPosition restPosition = WiperPosition.Left, holdPosition = WiperPosition.Left;
									List<string> daytimeDropFiles, nighttimeDropFiles;
									Color24 TransparentColor = Color24.Blue;
									double wipeSpeed = 1.0, holdTime = 1.0, dropLife = 10.0;
									try
									{
										daytimeDropFiles = Directory.GetFiles(Path.CombineDirectory(Program.FileSystem.DataFolder, "Compatibility\\Windscreen\\Day")).ToList();
										nighttimeDropFiles = Directory.GetFiles(Path.CombineDirectory(Program.FileSystem.DataFolder, "Compatibility\\Windscreen\\Night")).ToList();
									}
									catch
									{
										break;
									}

									while (i < Lines.Length && !(Lines[i].StartsWith("[", StringComparison.Ordinal) & Lines[i].EndsWith("]", StringComparison.Ordinal)))
									{
										int j = Lines[i].IndexOf('=');
										if (j >= 0)
										{
											int k;
											string Key = Lines[i].Substring(0, j).TrimEnd(new char[] { });
											string Value = Lines[i].Substring(j + 1).TrimStart(new char[] { });

											switch (Key.ToLowerInvariant())
											{
												case "topleft":
													k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out topLeft.X))
														{
															Interface.AddMessage(MessageType.Error, false, "X is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}

														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out topLeft.Y))
														{
															Interface.AddMessage(MessageType.Error, false, "Y is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													}
													else
													{
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}

													break;
												case "bottomright":
													k = Value.IndexOf(',');
													if (k >= 0)
													{
														string a = Value.Substring(0, k).TrimEnd(new char[] { });
														string b = Value.Substring(k + 1).TrimStart(new char[] { });
														if (a.Length != 0 && !NumberFormats.TryParseDoubleVb6(a, out bottomRight.X))
														{
															Interface.AddMessage(MessageType.Error, false, "X is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}

														if (b.Length != 0 && !NumberFormats.TryParseDoubleVb6(b, out bottomRight.Y))
														{
															Interface.AddMessage(MessageType.Error, false, "Y is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
														}
													}
													else
													{
														Interface.AddMessage(MessageType.Error, false, "Two arguments are expected in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}

													break;
												case "numberofdrops":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out numberOfDrops))
													{
														Interface.AddMessage(MessageType.Error, false, "NumberOfDrops is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}

													break;
												case "dropsize":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out dropSize))
													{
														Interface.AddMessage(MessageType.Error, false, "DropSize is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}

													break;
												case "daytimedrops":
													daytimeDropFiles = Value.IndexOf(',') != -1 ? Value.Trim().Split(',').ToList() : new List<string> {Value};
													break;
												case "nighttimedrops":
													nighttimeDropFiles = Value.IndexOf(',') != -1 ? Value.Trim().Split(',').ToList() : new List<string> {Value};
													break;
												case "transparentcolor":
													if (Value.Length != 0 && !Color24.TryParseHexColor(Value, out TransparentColor))
													{
														Interface.AddMessage(MessageType.Error, false, "HexColor is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}

													break;
												case "layer":
													if (Value.Length != 0 && !NumberFormats.TryParseIntVb6(Value, out Layer))
													{
														Interface.AddMessage(MessageType.Error, false, "NumberOfDrops is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}
													break;
												case "wipespeed":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out wipeSpeed))
													{
														Interface.AddMessage(MessageType.Error, false, "WipeSpeed is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}
													break;
												case "wiperholdtime":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out holdTime))
													{
														Interface.AddMessage(MessageType.Error, false, "WipeSpeed is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}
													break;
												case "restposition":
												case "wiperrestposition":
													switch (Value.ToLowerInvariant())
													{
														case "0":
														case "left":
															restPosition = WiperPosition.Left;
															break;
														case "1":
														case "right":
															restPosition = WiperPosition.Right;
															break;
														default:
															Interface.AddMessage(MessageType.Error, false, "WiperRestPosition is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															break;
													}
													break;
												case "holdposition":
												case "wiperholdposition":
													switch (Value.ToLowerInvariant())
													{
														case "0":
														case "left":
															holdPosition = WiperPosition.Left;
															break;
														case "1":
														case "right":
															holdPosition = WiperPosition.Right;
															break;
														default:
															Interface.AddMessage(MessageType.Error, false, "WiperHoldPosition is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
															break;
													}
													break;
												case "droplife":
													if (Value.Length != 0 && !NumberFormats.TryParseDoubleVb6(Value, out dropLife))
													{
														Interface.AddMessage(MessageType.Error, false, "DropLife is invalid in " + Key + " in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
													}
													break;
											}
										}

										i++;
									}

									i--;
									List<Texture> daytimeDrops = new List<Texture>(), nighttimeDrops = new List<Texture>();
									/*
									 * Ensure we have the same number of drops for day + night
									 * NOTE: If a drop is missing, we may get slightly odd effects, but can't be helped
									 * Raindrops ought to be blurry, and they're small enough anyway...
									 */
									int MD = Math.Max(daytimeDropFiles.Count, nighttimeDropFiles.Count);
									if (daytimeDropFiles.Count < MD)
									{
										while (daytimeDropFiles.Count < MD)
										{
											daytimeDropFiles.Add(string.Empty);
										}
									}

									if (nighttimeDropFiles.Count < MD)
									{
										while (nighttimeDropFiles.Count < MD)
										{
											nighttimeDropFiles.Add(string.Empty);
										}
									}

									for (int l = 0; l < daytimeDropFiles.Count; l++)
									{
										string currentDropFile = !System.IO.Path.IsPathRooted(daytimeDropFiles[l]) ? Path.CombineFile(TrainPath, daytimeDropFiles[l]) : daytimeDropFiles[l];
										if (!System.IO.File.Exists(currentDropFile))
										{
											currentDropFile = Path.CombineFile(Program.FileSystem.DataFolder, "Compatability\\Windscreen\\Day\\Drop" + Program.RandomNumberGenerator.Next(1, 4) + ".png");
											TransparentColor = Color24.Blue;
										}

										Texture drop;
										Program.Renderer.TextureManager.RegisterTexture(currentDropFile, new TextureParameters(null, TransparentColor), out drop);
										daytimeDrops.Add(drop);

									}

									for (int l = 0; l < nighttimeDropFiles.Count; l++)
									{
										string currentDropFile = !System.IO.Path.IsPathRooted(nighttimeDropFiles[l]) ? Path.CombineFile(TrainPath, nighttimeDropFiles[l]) : nighttimeDropFiles[l];
										if (!System.IO.File.Exists(currentDropFile))
										{
											currentDropFile = Path.CombineFile(Program.FileSystem.DataFolder, "Compatability\\Windscreen\\Night\\Drop" + Program.RandomNumberGenerator.Next(1, 4) + ".png");
											TransparentColor = Color24.Blue;
										}

										Texture drop;
										Program.Renderer.TextureManager.RegisterTexture(currentDropFile, new TextureParameters(null, TransparentColor), out drop);
										nighttimeDrops.Add(drop);
									}

									double dropInterval = (bottomRight.X - topLeft.X) / numberOfDrops;
									double currentDropX = topLeft.X;
									Train.Cars[Train.DriverCar].Windscreen = new Windscreen(numberOfDrops, dropLife, Train.Cars[Train.DriverCar]);
									Train.Cars[Train.DriverCar].Windscreen.Wipers = new WindscreenWiper(Train.Cars[Train.DriverCar].Windscreen, restPosition, holdPosition, wipeSpeed, holdTime);
									// Create drops
									for (int drop = 0; drop < numberOfDrops; drop++)
									{
										int DropTexture = Program.RandomNumberGenerator.Next(daytimeDrops.Count);
										double currentDropY = Program.RandomNumberGenerator.NextDouble() * (bottomRight.Y - topLeft.Y) + topLeft.Y;
										OpenBVEGame.RunInRenderThread(() =>
										{
											Program.CurrentHost.LoadTexture(daytimeDrops[DropTexture], OpenGlTextureWrapMode.ClampClamp);
										});
										int panelDropIndex = CreateElement(ref Train.Cars[Car].CarSections[0].Groups[0], currentDropX, currentDropY, dropSize, dropSize, new Vector2(0.5, 0.5), (double)Layer * StackDistance, PanelResolution, PanelTop, PanelBottom, PanelCenter, Train.Cars[Car].Driver, daytimeDrops[DropTexture], nighttimeDrops[DropTexture], Color32.White, false);
										string f = drop + " raindrop";
										try
										{
											Train.Cars[Car].CarSections[0].Groups[GroupIndex].Elements[panelDropIndex].StateFunction = new FunctionScript(Program.CurrentHost, f + " 1 == --", false);
										}
										catch
										{
											Program.CurrentHost.AddMessage(MessageType.Error, false, "Invalid animated function provided in " + Section + " at line " + (i + 1).ToString(Culture) + " in " + FileName);
										}

										currentDropX += dropInterval;
									}
								}
								break;
						}
					}
				}
			}
		}

		internal static string GetInfixFunction(TrainManager.Train Train, string Subject, double Minimum, double Maximum, int Width, int TextureWidth, string ErrorLocation)
		{
			double mp = 0.0;
			if (Minimum < 0)
			{
				mp = Math.Abs(Minimum);
			}
			double ftc = 1.0;
			if (Width != 0)
			{
				//If the width of the needle is not set, it will loop round to the starting position
				ftc -= (double)Width / TextureWidth;
			}
			double range = ftc / ((Maximum + mp) - (Minimum + mp));

			string subjectText = GetStackLanguageFromSubject(Train, Subject, ErrorLocation);

			if (!string.IsNullOrEmpty(subjectText))
			{
				return $"{subjectText} {Maximum} < {subjectText} {Minimum} > {subjectText} {Minimum + mp} - {range} * 0 ? {ftc} ?";
			}

			return string.Empty;
		}

		/// <summary>Converts a Panel2.cfg subject to an animation function stack</summary>
		/// <param name="Train">The train</param>
		/// <param name="Subject">The subject to convert</param>
		/// <param name="ErrorLocation">The location in the Panel2.cfg file</param>
		/// <returns>The parsed animation function stack</returns>
		internal static string GetStackLanguageFromSubject(TrainManager.Train Train, string Subject, string ErrorLocation) {
			System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.InvariantCulture;
			string Suffix = "";
			{
				// detect d# suffix
				int i;
				for (i = Subject.Length - 1; i >= 0; i--) {
					int a = char.ConvertToUtf32(Subject, i);
					if (a < 48 | a > 57) break;
				}
				if (i >= 0 & i < Subject.Length - 1) {
					if (Subject[i] == 'd' | Subject[i] == 'D') {
						int n;
						if (int.TryParse(Subject.Substring(i + 1), System.Globalization.NumberStyles.Integer, Culture, out n)) {
							if (n == 0) {
								Suffix = " floor 10 mod";
							} else {
								string t0 = Math.Pow(10.0, (double)n).ToString(Culture);
								string t1 = Math.Pow(10.0, (double)-n).ToString(Culture);
								Suffix = " ~ " + t0 + " >= <> " + t1 + " * floor 10 mod 10 ?";
							}
							Subject = Subject.Substring(0, i);
						}
					}
				}
			}
			// transform subject
			string Code;
			switch (Subject.ToLowerInvariant()) {
				case "acc":
					Code = "acceleration";
					break;
				case "motor":
					Code = "accelerationmotor";
					break;
				case "true":
					Code = "1";
					break;
				case "kmph":
					Code = "speedometer abs 3.6 *";
					break;
				case "mph":
					Code = "speedometer abs 2.2369362920544 *";
					break;
				case "ms":
					Code = "speedometer abs";
					break;
				case "locobrakecylinder":
					Code = Train.DriverCar + " brakecylinderindex 0.001 *";
					break;
				case "bc":
					Code = "brakecylinder 0.001 *";
					break;
				case "mr":
					Code = "mainreservoir 0.001 *";
					break;
				case "sap":
					Code = "straightairpipe 0.001 *";
					break;
				case "locobrakepipe":
					Code = Train.DriverCar + " brakepipeindex 0.001 *";
					break;
				case "bp":
					Code = "brakepipe 0.001 *";
					break;
				case "er":
					Code = "equalizingreservoir 0.001 *";
					break;
				case "door":
					Code = "1 doors -";
					break;
				case "csc":
					Code = "constSpeed";
					break;
				case "power":
					Code = "brakeNotchLinear 0 powerNotch ?";
					break;
				case "locobrake":
					Code = "locoBrakeNotch";
					break;
				case "brake":
					Code = "brakeNotchLinear";
					break;
				case "rev":
					Code = "reverserNotch ++";
					break;
				case "hour":
					Code = "0.000277777777777778 time * 24 mod floor";
					break;
				case "min":
					Code = "0.0166666666666667 time * 60 mod floor";
					break;
				case "sec":
					Code = "time 60 mod floor";
					break;
				case "atc":
					Code = "271 pluginstate";
					break;
				case "klaxon":
				case "horn":
					Code = "klaxon";
					break;
				case "primaryklaxon":
				case "primaryhorn":
					Code = "primaryklaxon";
					break;
				case "secondaryklaxon":
				case "secondaryhorn":
					Code = "secondaryklaxon";
					break;
				case "doorbuttonl":
					Code = "leftdoorbutton";
					break;
				case "doorbuttonr":
					Code = "rightdoorbutton";
					break;
				case "routelimit":
					Code = "routelimit";
					break;
				case "wiperposition":
					Code = "wiperposition";
					break;
				default:
					{
						Code = "0";
						bool unsupported = true;
						if (Subject.StartsWith("ats", StringComparison.OrdinalIgnoreCase)) {
							string a = Subject.Substring(3);
							int n; if (int.TryParse(a, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n)) {
								if (n >= 0 & n <= 255) {
									Code = n.ToString(Culture) + " pluginstate";
									unsupported = false;
								}
							}
						} else if (Subject.StartsWith("doorl", StringComparison.OrdinalIgnoreCase)) {
							string a = Subject.Substring(5);
							int n; if (int.TryParse(a, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n)) {
								if (n >= 0 & n < Train.Cars.Length) {
									Code = n.ToString(Culture) + " leftdoorsindex ceiling";
									unsupported = false;
								} else {
									Code = "2";
									unsupported = false;
								}
							}
						} else if (Subject.StartsWith("doorr", StringComparison.OrdinalIgnoreCase)) {
							string a = Subject.Substring(5);
							int n; if (int.TryParse(a, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n)) {
								if (n >= 0 & n < Train.Cars.Length) {
									Code = n.ToString(Culture) + " rightdoorsindex ceiling";
									unsupported = false;
								} else {
									Code = "2";
									unsupported = false;
								}
							}
						}
						if (unsupported) {
							Interface.AddMessage(MessageType.Error, false, "Invalid subject " + Subject + " encountered in " + ErrorLocation);
						}
					} break;
			}
			return Code + Suffix;
		}

		

		internal static int CreateElement(ref ElementsGroup Group, double Left, double Top, double Width, double Height, Vector2 RelativeRotationCenter, double Distance, double PanelResolution, double PanelTop, double PanelBottom, Vector2 PanelCenter, Vector3 Driver, Texture DaytimeTexture, Texture NighttimeTexture, Color32 Color, bool AddStateToLastElement)
		{
			double WorldWidth, WorldHeight;
			if (Program.Renderer.Screen.Width >= Program.Renderer.Screen.Height) {
				WorldWidth = 2.0 * Math.Tan(0.5 * Program.Renderer.Camera.HorizontalViewingAngle) * EyeDistance;
				WorldHeight = WorldWidth / Program.Renderer.Screen.AspectRatio;
			} else {
				WorldHeight = 2.0 * Math.Tan(0.5 * Program.Renderer.Camera.VerticalViewingAngle) * EyeDistance / Program.Renderer.Screen.AspectRatio;
				WorldWidth = WorldHeight * Program.Renderer.Screen.AspectRatio;
			}
			double x0 = Left / PanelResolution;
			double x1 = (Left + Width) / PanelResolution;
			double y0 = (PanelBottom - Top) / PanelResolution * Program.Renderer.Screen.AspectRatio;
			double y1 = (PanelBottom - (Top + Height)) / PanelResolution * Program.Renderer.Screen.AspectRatio;
			double xd = 0.5 - PanelCenter.X / PanelResolution;
			x0 += xd; x1 += xd;
			double yt = PanelBottom - PanelResolution / Program.Renderer.Screen.AspectRatio;
			double yd = (PanelCenter.Y - yt) / (PanelBottom - yt) - 0.5;
			y0 += yd; y1 += yd;
			x0 = (x0 - 0.5) * WorldWidth;
			x1 = (x1 - 0.5) * WorldWidth;
			y0 = (y0 - 0.5) * WorldHeight;
			y1 = (y1 - 0.5) * WorldHeight;
			double xm = x0 * (1.0 - RelativeRotationCenter.X) + x1 * RelativeRotationCenter.X;
			double ym = y0 * (1.0 - RelativeRotationCenter.Y) + y1 * RelativeRotationCenter.Y;
			Vector3[] v = new Vector3[4];
			v[0] = new Vector3(x0 - xm, y1 - ym, 0);
			v[1] = new Vector3(x0 - xm, y0 - ym, 0);
			v[2] = new Vector3(x1 - xm, y0 - ym, 0);
			v[3] = new Vector3(x1 - xm, y1 - ym, 0);
			Vertex t0 = new Vertex(v[0], new Vector2(0.0f, 1.0f));
			Vertex t1 = new Vertex(v[1], new Vector2(0.0f, 0.0f));
			Vertex t2 = new Vertex(v[2], new Vector2(1.0f, 0.0f));
			Vertex t3 = new Vertex(v[3], new Vector2(1.0f, 1.0f));
			StaticObject Object = new StaticObject(Program.CurrentHost);
			Object.Mesh.Vertices = new VertexTemplate[] { t0, t1, t2, t3 };
			Object.Mesh.Faces = new MeshFace[] { new MeshFace(new int[] { 0, 1, 2, 3 }) };
			Object.Mesh.Materials = new MeshMaterial[1];
			Object.Mesh.Materials[0].Flags = new MaterialFlags();
			if (DaytimeTexture != null)
			{
				Object.Mesh.Materials[0].Flags |= MaterialFlags.TransparentColor;

				if (NighttimeTexture != null)
				{
					// In BVE4 and versions of OpenBVE prior to v1.7.1.0, elements with NighttimeImage defined are rendered with lighting disabled.
					Object.Mesh.Materials[0].Flags |= MaterialFlags.DisableLighting;
				}
			}
			Object.Mesh.Materials[0].Color = Color;
			Object.Mesh.Materials[0].TransparentColor = Color24.Blue;
			Object.Mesh.Materials[0].DaytimeTexture = DaytimeTexture;
			Object.Mesh.Materials[0].NighttimeTexture = NighttimeTexture;
			Object.Dynamic = true;
			// calculate offset
			Vector3 o;
			o.X = xm + Driver.X;
			o.Y = ym + Driver.Y;
			o.Z = EyeDistance - Distance + Driver.Z;
			// add object
			if (AddStateToLastElement) {
				int n = Group.Elements.Length - 1;
				int j = Group.Elements[n].States.Length;
				Array.Resize(ref Group.Elements[n].States, j + 1);
				Group.Elements[n].States[j] = new ObjectState
				{
					Translation = Matrix4D.CreateTranslation(o.X, o.Y, -o.Z),
					Prototype = Object
				};
				return n;
			} else {
				int n = Group.Elements.Length;
				Array.Resize(ref Group.Elements, n + 1);
				Group.Elements[n] = new AnimatedObject(Program.CurrentHost);
				Group.Elements[n].States = new[] { new ObjectState() };
				Group.Elements[n].States[0].Translation = Matrix4D.CreateTranslation(o.X, o.Y, -o.Z);
				Group.Elements[n].States[0].Prototype = Object;
				Group.Elements[n].CurrentState = 0;
				Group.Elements[n].internalObject = new ObjectState { Prototype = Object };
				Program.CurrentHost.CreateDynamicObject(ref Group.Elements[n].internalObject);
				return n;
			}
		}

	}
}
