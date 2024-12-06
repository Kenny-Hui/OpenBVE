//Simplified BSD License (BSD-2-Clause)
//
//Copyright (c) 2024, Christopher Lees, The OpenBVE Project
//
//Redistribution and use in source and binary forms, with or without
//modification, are permitted provided that the following conditions are met:
//
//1. Redistributions of source code must retain the above copyright notice, this
//   list of conditions and the following disclaimer.
//2. Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
//ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
//WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
//DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
//ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
//(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
//LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
//ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
//(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
//SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using OpenBve.Formats.MsTs;
using OpenBveApi.Colors;
using OpenBveApi.Interface;
using OpenBveApi.Math;
using OpenBveApi.Objects;
using OpenBveApi.Textures;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Deflate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Stop ReSharper complaining about unused stuff:
// We need to load this sequentially anyway, and
// hopefully this will be used in a later build

// ReSharper disable NotAccessedField.Local
// ReSharper disable RedundantAssignment
// ReSharper disable UnusedVariable
#pragma warning disable 0219
namespace Plugin
{
	class MsTsShapeParser
	{
		struct Texture
		{
			internal string fileName;
			internal int filterMode;
			internal int mipmapLODBias;
			internal Color32 borderColor;
		}

		struct PrimitiveState
		{
			internal string Name;
			internal UInt32 Flags;
			internal int Shader;
			internal int[] Textures;
			/*
			 * Unlikely to be able to support these at present
			 * However, read and see if we can hack common ones
			 */
			internal float ZBias;
			internal int vertexStates;
			internal int alphaTestMode;
			internal int lightCfgIdx;
			internal int zBufferMode;
		}

		struct VertexStates
		{
			internal uint flags; //Describes specular and some other stuff, unlikely to be supported
			internal int hierarchyID; //The hierarchy ID of the top-level transform matrix, remember that they chain
			internal int lightingMatrixID;
			internal int lightingConfigIdx;
			internal uint lightingFlags;
			internal int matrix2ID; //Optional
		}

		struct VertexSet
		{
			internal int hierarchyIndex;
			internal int startVertex;
			internal int numVerticies;
		}


		struct Animation
		{
			internal int FrameCount;
			internal Dictionary<string, KeyframeAnimation> Nodes;
			/*
			 * WARNING: MSTS glitch / 'feature':
			 * This FPS number is divided by 30 for interior view objects
			 * http://www.elvastower.com/forums/index.php?/topic/29692-animations-in-the-passenger-view-too-fast/page__p__213634
			 */
			internal double FrameRate;
		}

		
		class Vertex
		{
			internal Vector3 Coordinates;
			internal Vector3 Normal;
			internal Vector2 TextureCoordinates;
			internal int[] matrixChain;
			internal bool alreadyTransformed;

			public Vertex(Vector3 c, Vector3 n)
			{
				this.Coordinates = new Vector3(c.X, c.Y, c.Z);
				this.Normal = new Vector3(n.X, n.Y, n.Z);
			}
		}

		private struct Face
		{
			internal readonly int[] Vertices;
			internal readonly int Material;

			internal Face(int[] vertices, int material)
			{
				Vertices = vertices;
				if (material == -1)
				{
					Material = 0;
				}
				else
				{
					Material = material;
				}

			}
		}

		private class MsTsShape
		{
			internal MsTsShape()
			{
				points = new List<Vector3>();
				normals = new List<Vector3>();
				uv_points = new List<Vector2>();
				images = new List<string>();
				textures = new List<Texture>();
				prim_states = new List<PrimitiveState>();
				vtx_states = new List<VertexStates>();
				LODs = new List<LOD>();
				Animations = new List<Animation>();
				AnimatedMatricies = new Dictionary<int, int>();
				Matricies = new List<KeyframeMatrix>();
			}

			// Global variables used by all LODs

			/// <summary>The points used by this shape</summary>
			internal readonly List<Vector3> points;
			/// <summary>The normal vectors used by this shape</summary>
			internal readonly List<Vector3> normals;
			/// <summary>The texture-coordinates used by this shape</summary>
			internal readonly List<Vector2> uv_points;
			/// <summary>The filenames of all textures used by this shape</summary>
			internal readonly List<string> images;
			/// <summary>The textures used, with associated parameters</summary>
			/// <remarks>Allows the alpha testing mode to be set etc. so that the same image file can be reused</remarks>
			internal readonly List<Texture> textures;
			/// <summary>Contains the shader, texture etc. used by the primitive</summary>
			/// <remarks>Largely unsupported other than the texture name at the minute</remarks>
			internal readonly List<PrimitiveState> prim_states;

			internal readonly List<VertexStates> vtx_states;

			internal readonly List<Animation> Animations;
			/// <summary>Dictionary of animated matricies mapped to the final model</summary>
			/// <remarks>Most matricies aren't animated, so don't send excessive numbers to the shader each frame</remarks>
			internal readonly Dictionary<int, int> AnimatedMatricies;
			/// <summary>The matricies within the shape</summary>
			internal List<KeyframeMatrix> Matricies;

			// The list of LODs actually containing the objects

			/// <summary>The list of all LODs from the model</summary>
			internal readonly List<LOD> LODs;

			// Control variables

			internal int currentPrimitiveState = -1;
			internal int totalObjects = 0;
			internal int currentFrame = 0;
		}

		private class LOD
		{
			/// <summary>Creates a new LOD</summary>
			/// <param name="distance">The maximum viewing distance for this LOD</param>
			internal LOD(double distance)
			{
				this.viewingDistance = distance;
				this.subObjects = new List<SubObject>();
			}

			internal readonly double viewingDistance;
			internal List<SubObject> subObjects;
			internal int[] hierarchy;
		}

		private class SubObject
		{
			internal SubObject()
			{
				this.verticies = new List<Vertex>();
				this.vertexSets = new List<VertexSet>();
				this.faces = new List<Face>();
				this.materials = new List<Material>();
				this.hierarchy = new List<int>();
			}

			internal void TransformVerticies(MsTsShape shape)
			{
				transformedVertices = new List<Vertex>(verticies);
				
				
				for (int i = 0; i < verticies.Count; i++)
				{
					transformedVertices[i] = new Vertex(verticies[i].Coordinates, verticies[i].Normal);
					for (int j = 0; j < vertexSets.Count; j++)
					{
						if (vertexSets[j].startVertex <= i && vertexSets[j].startVertex + vertexSets[j].numVerticies > i)
						{
							List<int> matrixChain = new List<int>();
							bool staticTransform = true;
							int hi = vertexSets[j].hierarchyIndex;
							if (hi != -1 && hi < shape.Matricies.Count)
							{
								matrixChain.Add(hi);
								while (hi != -1)
								{
									hi = currentLOD.hierarchy[hi];
									if (hi == -1)
									{
										break;
									}

									if (hi == matrixChain[0])
									{
										continue;
									}
									matrixChain.Insert(0, hi);
									if (IsAnimated(shape.Matricies[hi].Name) && staticTransform)
									{
										staticTransform = false;
									}
								}
							}
							else
							{
								//Unsure of the cause of this, matrix appears to be invalid
								i = vertexSets[j].startVertex + vertexSets[j].numVerticies;
								matrixChain.Clear();
							}

							if (staticTransform)
							{
								for (int k = 0; k < matrixChain.Count; k++)
								{
									transformedVertices[i].Coordinates.Transform(shape.Matricies[matrixChain[k]].Matrix, false);
								}
							}
							else
							{
								/*
								 * Check if our matricies are in the shape, and copy them there if not
								 *
								 * Note:
								 * ----
								 * This is trading off a slightly slower load time for not copying large numbers of matricies to the shader each time,
								 * so better FPS whilst running the thing
								 */
								for (int k = 0; k < matrixChain.Count; k++)
								{
									if (shape.AnimatedMatricies.ContainsKey(matrixChain[k]))
									{
										// replace with the actual index in the shape matrix array
										matrixChain[k] = shape.AnimatedMatricies[matrixChain[k]];
									}
									else
									{
										// copy matrix to shape and add to our dict
										int matrixIndex = newResult.Matricies.Length;
										Array.Resize(ref newResult.Matricies, matrixIndex + 1);
										newResult.Matricies[matrixIndex] = shape.Matricies[matrixChain[k]];
										shape.AnimatedMatricies.Add(matrixChain[k], matrixIndex);
										matrixChain[k] = matrixIndex;
									}
								}

								// Note: transforming verticies must be done in reverse if the model is in motion
								matrixChain.Reverse();
								// used to pack 4 x matrix indicies into a int
								int[] transformChain = { 255, 255, 255, 255};
								matrixChain.CopyTo(transformChain);
								transformedVertices[i].matrixChain = transformChain;

							}

							break;
						}
					}
				}
			}

			internal void Apply(out StaticObject Object, bool useTransformedVertics)
			{
				Object = new StaticObject(Plugin.currentHost)
				{
					Mesh =
					{
						Faces = new MeshFace[] { },
						Materials = new MeshMaterial[] { },
						Vertices = new VertexTemplate[] { }
					}
				};
				if (faces.Count != 0)
				{
					int mf = Object.Mesh.Faces.Length;
					int mm = Object.Mesh.Materials.Length;
					int mv = Object.Mesh.Vertices.Length;
					Array.Resize(ref Object.Mesh.Faces, mf + faces.Count);
					Array.Resize(ref Object.Mesh.Materials, mm + materials.Count);
					Array.Resize(ref Object.Mesh.Vertices, mv + verticies.Count);
					for (int i = 0; i < verticies.Count; i++)
					{
						if (useTransformedVertics)
						{
							//Use transformed vertices if we are not animated as will be faster
							if (transformedVertices[i].matrixChain != null)
							{
								Object.Mesh.Vertices[mv + i] = new AnimatedVertex(transformedVertices[i].Coordinates, verticies[i].TextureCoordinates, transformedVertices[i].matrixChain);
							}
							else
							{
								Object.Mesh.Vertices[mv + i] = new OpenBveApi.Objects.Vertex(transformedVertices[i].Coordinates, verticies[i].TextureCoordinates);
							}
							
						}
						else
						{
							Object.Mesh.Vertices[mv + i] = new OpenBveApi.Objects.Vertex(verticies[i].Coordinates, verticies[i].TextureCoordinates);
						}

					}

					for (int i = 0; i < faces.Count; i++)
					{
						Object.Mesh.Faces[i] = new MeshFace(faces[i].Vertices);
						Object.Mesh.Faces[i].Material = (ushort)faces[i].Material;
						for (int k = 0; k < faces[i].Vertices.Length; k++)
						{
							Object.Mesh.Faces[i].Vertices[k].Normal = verticies[faces[i].Vertices[k]].Normal;
						}

						for (int j = 0; j < Object.Mesh.Faces[mf + i].Vertices.Length; j++)
						{
							Object.Mesh.Faces[mf + i].Vertices[j].Index += (ushort)mv;
						}

						Object.Mesh.Faces[mf + i].Material += (ushort)mm;
					}

					for (int i = 0; i < materials.Count; i++)
					{
						Object.Mesh.Materials[mm + i].Flags = 0;
						Object.Mesh.Materials[mm + i].Color = materials[i].Color;
						Object.Mesh.Materials[mm + i].TransparentColor = Color24.Black;
						Object.Mesh.Materials[mm + i].BlendMode = MeshMaterialBlendMode.Normal;
						if (materials[i].DaytimeTexture != null)
						{
							OpenBveApi.Textures.Texture tday;
							Plugin.currentHost.RegisterTexture(materials[i].DaytimeTexture, new TextureParameters(null, null), out tday);
							Object.Mesh.Materials[mm + i].DaytimeTexture = tday;
						}
						else
						{
							Object.Mesh.Materials[mm + i].DaytimeTexture = null;
						}

						Object.Mesh.Materials[mm + i].EmissiveColor = materials[i].EmissiveColor;
						Object.Mesh.Materials[mm + i].NighttimeTexture = null;
						Object.Mesh.Materials[mm + i].GlowAttenuationData = materials[i].GlowAttenuationData;
						Object.Mesh.Materials[mm + i].WrapMode = materials[i].WrapMode;
					}
				}
			}

			internal readonly List<Vertex> verticies;
			internal readonly List<VertexSet> vertexSets;
			internal readonly List<Face> faces;
			internal readonly List<Material> materials;
			internal List<int> hierarchy;
			internal List<Vertex> transformedVertices;
		}

		private static string currentFolder;

		private static KeyframeAnimatedObject newResult;

		private static bool IsAnimated(string matrixName)
		{
			if (matrixName.StartsWith("WHEELS") || matrixName.StartsWith("ROD") || matrixName.StartsWith("BOGIE"))
			{
				return true;
			}

			return false;
		}

		internal static UnifiedObject ReadObject(string fileName)
		{
			MsTsShape shape = new MsTsShape();
			AnimatedObjectCollection Result = new AnimatedObjectCollection(Plugin.currentHost)
			{
				Objects = new AnimatedObject[4]
			};

			newResult = new KeyframeAnimatedObject(Plugin.currentHost);

			currentFolder = Path.GetDirectoryName(fileName);
			Stream fb = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

			byte[] buffer = new byte[34];
			fb.Read(buffer, 0, 2);

			bool unicode = (buffer[0] == 0xFF && buffer[1] == 0xFE);

			string headerString;
			if (unicode)
			{
				fb.Read(buffer, 0, 32);
				headerString = Encoding.Unicode.GetString(buffer, 0, 16);
			}
			else
			{
				fb.Read(buffer, 2, 14);
				headerString = Encoding.ASCII.GetString(buffer, 0, 8);
			}

			// SIMISA@F  means compressed
			// SIMISA@@  means uncompressed
			if (headerString.StartsWith("SIMISA@F"))
			{
				fb = new ZlibStream(fb, CompressionMode.Decompress);
			}
			else if (headerString.StartsWith("\r\nSIMISA"))
			{
				// ie us1rd2l1000r10d.s, we are going to allow this but warn
				Console.Error.WriteLine("Improper header in " + fileName);
				fb.Read(buffer, 0, 4);
			}
			else if (!headerString.StartsWith("SIMISA@@"))
			{
				throw new Exception("Unrecognized shape file header " + headerString + " in " + fileName);
			}

			string subHeader;
			if (unicode)
			{
				fb.Read(buffer, 0, 32);
				subHeader = Encoding.Unicode.GetString(buffer, 0, 16);
			}
			else
			{
				fb.Read(buffer, 0, 16);
				subHeader = Encoding.ASCII.GetString(buffer, 0, 8);
			}
			if (subHeader[7] == 't')
			{
				using (BinaryReader reader = new BinaryReader(fb))
				{
					byte[] newBytes = reader.ReadBytes((int)(fb.Length - fb.Position));
					string s;
					if (unicode)
					{
						s = Encoding.Unicode.GetString(newBytes);
					}
					else
					{
						s = Encoding.ASCII.GetString(newBytes);
					}
					TextualBlock block = new TextualBlock(s, KujuTokenID.shape);
					ParseBlock(block, ref shape);
				}

			}
			else if (subHeader[7] != 'b')
			{
				throw new Exception("Unrecognized subHeader \"" + subHeader + "\" in " + fileName);
			}
			else
			{
				using (BinaryReader reader = new BinaryReader(fb))
				{
					KujuTokenID currentToken = (KujuTokenID)reader.ReadUInt16();
					if (currentToken != KujuTokenID.shape)
					{
						throw new Exception(); //Shape definition
					}
					reader.ReadUInt16();
					uint remainingBytes = reader.ReadUInt32();
					byte[] newBytes = reader.ReadBytes((int)remainingBytes);
					BinaryBlock block = new BinaryBlock(newBytes, KujuTokenID.shape);
					ParseBlock(block, ref shape);
				}
			}
			Array.Resize(ref Result.Objects, shape.totalObjects);
			Array.Resize(ref newResult.Objects, shape.totalObjects);
			int idx = 0;
			double[] previousLODs = new double[shape.totalObjects];
			for (int i = 0; i < shape.LODs.Count; i++)
			{
				for (int j = 0; j < shape.LODs[i].subObjects.Count; j++)
				{
					ObjectState aos = new ObjectState();
					if (i == 0)
					{
						shape.LODs[i].subObjects[j].Apply(out aos.Prototype, true);
						previousLODs[idx] = shape.LODs[i].viewingDistance;
						int k = idx;
						while (k > 0)
						{
							if (previousLODs[k] < shape.LODs[i].viewingDistance)
							{
								break;
							}

							k--;

						}
						/*
						 * END TO REMOVE
						 */


						newResult.Objects[idx] = aos;
						idx++;
					}
					
				}
			}

			Matrix4D matrix = Matrix4D.Identity;
			

			return newResult;
		}

		private static LOD currentLOD;

		private static void ParseBlock(Block block, ref MsTsShape shape)
		{
			Vertex v = null; //Crappy, but there we go.....
			int[] t = null;
			KeyframeAnimation node = null;
			ParseBlock(block, ref shape, ref v, ref t, ref node);
		}

		private static void ParseBlock(Block block, ref MsTsShape shape, ref int[] array)
		{
			Vertex v = null; //Crappy, but there we go.....
			KeyframeAnimation node = null;
			ParseBlock(block, ref shape, ref v, ref array, ref node);
		}

		private static void ParseBlock(Block block, ref MsTsShape shape, ref Vertex v)
		{
			int[] t = null;
			KeyframeAnimation node = null;
			KeyframeAnimation animation = null;
			ParseBlock(block, ref shape, ref v, ref t, ref animation);
		}

		private static void ParseBlock(Block block, ref MsTsShape shape, ref KeyframeAnimation n)
		{
			Vertex v = null;
			int[] t = null;
			ParseBlock(block, ref shape, ref v, ref t, ref n);
		}

		private static int NumFrames;

		private static QuaternionFrame[] quaternionFrames;

		private static VectorFrame[] vectorFrames;

		private static int controllerIndex;

		private static int currentFrame;

		private static void ParseBlock(Block block, ref MsTsShape shape, ref Vertex vertex, ref int[] intArray, ref KeyframeAnimation animationNode)
		{
			float x, y, z;
			Vector3 point;
			KujuTokenID currentToken = KujuTokenID.error;
			Block newBlock;
			uint flags;

			switch (block.Token)
			{
				case KujuTokenID.shape:
					newBlock = block.ReadSubBlock(KujuTokenID.shape_header);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.volumes);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.shader_names);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.texture_filter_names);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.points);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.uv_points);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.normals);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.sort_vectors);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.colours);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.matrices);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.images);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.textures);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.light_materials);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.light_model_cfgs);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.vtx_states);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.prim_states);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.lod_controls);
					ParseBlock(newBlock, ref shape);
					try
					{
						newBlock = block.ReadSubBlock(KujuTokenID.animations);
						ParseBlock(newBlock, ref shape);
					}
					catch (EndOfStreamException)
					{
						// Animation controllers are optional
					}
					break;
				case KujuTokenID.shape_header:
				case KujuTokenID.volumes:
				case KujuTokenID.texture_filter_names:
				case KujuTokenID.sort_vectors:
				case KujuTokenID.colours:
				case KujuTokenID.light_materials:
				case KujuTokenID.light_model_cfgs:
					//Unsupported stuff, so just read to the end at the minute
					block.Skip((int)block.Length());
					break;

				case KujuTokenID.vtx_state:
					flags = block.ReadUInt32();
					int matrix1 = block.ReadInt32();
					int lightMaterialIdx = block.ReadInt32();
					int lightStateCfgIdx = block.ReadInt32();
					uint lightFlags = block.ReadUInt32();
					int matrix2 = -1;
					if ((block is BinaryBlock && block.Length() - block.Position() > 1) || (!(block is BinaryBlock) && block.Length() - block.Position() > 2))
					{
						matrix2 = block.ReadInt32();
					}

					VertexStates vs = new VertexStates();
					vs.flags = flags;
					vs.hierarchyID = matrix1;
					vs.lightingMatrixID = lightMaterialIdx;
					vs.lightingConfigIdx = lightStateCfgIdx;
					vs.lightingFlags = lightFlags;
					vs.matrix2ID = matrix2;
					shape.vtx_states.Add(vs);
					break;
				case KujuTokenID.vtx_states:
					int vtxStateCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (vtxStateCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.vtx_state);
						ParseBlock(newBlock, ref shape);
						vtxStateCount--;
					}

					break;
				case KujuTokenID.prim_state:
					flags = block.ReadUInt32();
					int shader = block.ReadInt32();
					int[] texIdxs = { };
					newBlock = block.ReadSubBlock(KujuTokenID.tex_idxs);
					ParseBlock(newBlock, ref shape, ref texIdxs);

					float zBias = block.ReadSingle();
					int vertexStates = block.ReadInt32();
					int alphaTestMode = block.ReadInt32();
					int lightCfgIdx = block.ReadInt32();
					int zBufferMode = block.ReadInt32();
					PrimitiveState p = new PrimitiveState();
					p.Name = block.Label;
					p.Flags = flags;
					p.Shader = shader;
					p.Textures = texIdxs;
					p.ZBias = zBias;
					p.vertexStates = vertexStates;
					p.alphaTestMode = alphaTestMode;
					p.lightCfgIdx = lightCfgIdx;
					p.zBufferMode = zBufferMode;
					shape.prim_states.Add(p);
					break;
				case KujuTokenID.tex_idxs:
					int texIdxCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					Array.Resize(ref intArray, texIdxCount);
					int idx = 0;
					while (texIdxCount > 0)
					{
						intArray[idx] = block.ReadUInt16();
						idx++;
						texIdxCount--;
					}
					break;
				case KujuTokenID.prim_states:
					int primStateCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (primStateCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.prim_state);
						ParseBlock(newBlock, ref shape);
						primStateCount--;
					}

					break;
				case KujuTokenID.texture:
					int imageIDX = block.ReadInt32();
					int filterMode = (int)block.ReadUInt32();
					float mipmapLODBias = block.ReadSingle();
					uint borderColor = 0xff000000U;
					if (block.Length() - block.Position() > 1)
					{
						borderColor = block.ReadUInt32();
					}

					//Unpack border color
					float r, g, b, a;
					r = borderColor % 256;
					g = (borderColor / 256) % 256;
					b = (borderColor / 256 / 256) % 256;
					a = (borderColor / 256 / 256 / 256) % 256;
					Texture t = new Texture();
					t.fileName = shape.images[imageIDX];
					t.filterMode = filterMode;
					t.mipmapLODBias = (int)mipmapLODBias;
					t.borderColor = new Color32((byte)r, (byte)g, (byte)b, (byte)a);
					shape.textures.Add(t);
					break;
				case KujuTokenID.textures:
					int textureCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (textureCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.texture);
						ParseBlock(newBlock, ref shape);
						textureCount--;
					}

					break;
				case KujuTokenID.image:
					shape.images.Add(block.ReadString());
					break;
				case KujuTokenID.images:
					int imageCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (imageCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.image);
						ParseBlock(newBlock, ref shape);
						imageCount--;
					}

					break;
				case KujuTokenID.cullable_prims:
					int numPrims = block.ReadInt32();
					int numFlatSections = block.ReadInt32();
					int numPrimIdxs = block.ReadInt32();
					break;
				case KujuTokenID.geometry_info:
					int faceNormals = block.ReadInt32();
					int txLightCommands = block.ReadInt32();
					int nodeXTrilistIdxs = block.ReadInt32();
					int trilistIdxs = block.ReadInt32();
					int lineListIdxs = block.ReadInt32();
					nodeXTrilistIdxs = block.ReadInt32(); //Duped, or is the first one actually something else?
					int trilists = block.ReadInt32();
					int lineLists = block.ReadInt32();
					int pointLists = block.ReadInt32();
					int nodeXTrilists = block.ReadInt32();
					newBlock = block.ReadSubBlock(KujuTokenID.geometry_nodes);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.geometry_node_map);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.geometry_node_map:
					int[] geometryNodes = new int[block.ReadInt32()];
					for (int i = 0; i < geometryNodes.Length; i++)
					{
						geometryNodes[i] = block.ReadInt32();
					}

					break;
				case KujuTokenID.geometry_node:
					int n_txLightCommands = block.ReadInt32();
					int n_nodeXTxLightCmds = block.ReadInt32();
					int n_trilists = block.ReadInt32();
					int n_lineLists = block.ReadInt32();
					int n_pointLists = block.ReadInt32();
					newBlock = block.ReadSubBlock(KujuTokenID.cullable_prims);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.geometry_nodes:
					int geometryNodeCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (geometryNodeCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.geometry_node);
						ParseBlock(newBlock, ref shape);
						geometryNodeCount--;
					}

					break;
				case KujuTokenID.point:
					x = block.ReadSingle();
					y = block.ReadSingle();
					z = block.ReadSingle();
					point = new Vector3(x, y, z);
					shape.points.Add(point);
					break;
				case KujuTokenID.vector:
					x = block.ReadSingle();
					y = block.ReadSingle();
					z = block.ReadSingle();
					point = new Vector3(x, y, z);
					shape.normals.Add(point);
					break;
				case KujuTokenID.points:
					int pointCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (pointCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.point);
						ParseBlock(newBlock, ref shape);
						pointCount--;
					}

					break;
				case KujuTokenID.uv_point:
					x = block.ReadSingle();
					y = block.ReadSingle();
					var uv_point = new Vector2(x, y);
					shape.uv_points.Add(uv_point);
					break;
				case KujuTokenID.uv_points:
					int uvPointCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (uvPointCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.uv_point);
						ParseBlock(newBlock, ref shape);
						uvPointCount--;
					}

					break;
				case KujuTokenID.matrices:
					int matrixCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (matrixCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.matrix);
						ParseBlock(newBlock, ref shape);
						matrixCount--;
					}
					break;
				case KujuTokenID.matrix:
					Matrix4D currentMatrix = new Matrix4D();
					currentMatrix.Row0 = new Vector4(block.ReadSingle(), block.ReadSingle(), block.ReadSingle(), 0);
					currentMatrix.Row1 = new Vector4(block.ReadSingle(), block.ReadSingle(), block.ReadSingle(), 0);
					currentMatrix.Row2 = new Vector4(block.ReadSingle(), block.ReadSingle(), block.ReadSingle(), 0);
					currentMatrix.Row3 = new Vector4(block.ReadSingle(), block.ReadSingle(), block.ReadSingle(), 0);
					shape.Matricies.Add(new KeyframeMatrix(newResult, block.Label, currentMatrix));
					break;
				case KujuTokenID.normals:
					int normalCount = block.ReadUInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (normalCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.vector);
						ParseBlock(newBlock, ref shape);
						normalCount--;
					}

					break;
				case KujuTokenID.distance_levels_header:
					int DLevBias = block.ReadInt16();
					break;
				case KujuTokenID.distance_levels:
					int distanceLevelCount = block.ReadInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (distanceLevelCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.distance_level);
						ParseBlock(newBlock, ref shape);
						distanceLevelCount--;
					}

					break;
				case KujuTokenID.distance_level_header:
					newBlock = block.ReadSubBlock(KujuTokenID.dlevel_selection);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.hierarchy);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.distance_level:
					newBlock = block.ReadSubBlock(KujuTokenID.distance_level_header);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.sub_objects);
					ParseBlock(newBlock, ref shape);
					shape.LODs.Add(currentLOD);
					break;
				case KujuTokenID.dlevel_selection:
					currentLOD = new LOD(block.ReadSingle());
					break;
				case KujuTokenID.hierarchy:
					currentLOD.hierarchy = new int[block.ReadInt32()];
					for (int i = 0; i < currentLOD.hierarchy.Length; i++)
					{
						currentLOD.hierarchy[i] = block.ReadInt32();
					}

					break;
				case KujuTokenID.lod_control:
					newBlock = block.ReadSubBlock(KujuTokenID.distance_levels_header);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.distance_levels);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.lod_controls:
					int lodCount = block.ReadInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (lodCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.lod_control);
						ParseBlock(newBlock, ref shape);
						lodCount--;
					}

					break;
				case KujuTokenID.primitives:
					int capacity = block.ReadInt32(); //Count of the number of entries in the block, not the number of primitives
					while (capacity > 0)
					{
						newBlock = block.ReadSubBlock(new[] { KujuTokenID.prim_state_idx, KujuTokenID.indexed_trilist });
						switch (newBlock.Token)
						{
							case KujuTokenID.prim_state_idx:
								ParseBlock(newBlock, ref shape);
								string txF = null;
								try
								{
									txF = OpenBveApi.Path.CombineFile(currentFolder, shape.textures[shape.prim_states[shape.currentPrimitiveState].Textures[0]].fileName);
									if (!File.Exists(txF))
									{
										Plugin.currentHost.AddMessage(MessageType.Warning, true, "Texture file " + shape.textures[shape.prim_states[shape.currentPrimitiveState].Textures[0]].fileName + " was not found.");
										txF = null;
									}
								}
								catch
								{
									Plugin.currentHost.AddMessage(MessageType.Warning, true, "Texture file path " + shape.textures[shape.prim_states[shape.currentPrimitiveState].Textures[0]].fileName + " was invalid.");
								}
								currentLOD.subObjects[currentLOD.subObjects.Count - 1].materials.Add(new Material(txF));
								break;
							case KujuTokenID.indexed_trilist:
								ParseBlock(newBlock, ref shape);
								break;
							default:
								throw new Exception("Unexpected primitive type, got " + currentToken);
						}

						capacity--;
					}

					break;
				case KujuTokenID.prim_state_idx:
					shape.currentPrimitiveState = block.ReadInt32();
					break;
				case KujuTokenID.indexed_trilist:
					newBlock = block.ReadSubBlock(KujuTokenID.vertex_idxs);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.normal_idxs);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.sub_object:
					newBlock = block.ReadSubBlock(KujuTokenID.sub_object_header);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.vertices);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.vertex_sets);
					ParseBlock(newBlock, ref shape);
					newBlock = block.ReadSubBlock(KujuTokenID.primitives);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.sub_objects:
					int subObjectCount = block.ReadInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (subObjectCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.sub_object);
						ParseBlock(newBlock, ref shape);
						subObjectCount--;
					}

					break;
				case KujuTokenID.sub_object_header:
					currentLOD.subObjects.Add(new SubObject());
					shape.totalObjects++;
					flags = block.ReadUInt32();
					int sortVectorIdx = block.ReadInt32();
					int volIdx = block.ReadInt32();
					uint sourceVertexFormatFlags = block.ReadUInt32();
					uint destinationVertexFormatFlags = block.ReadUInt32();
					newBlock = block.ReadSubBlock(KujuTokenID.geometry_info);
					ParseBlock(newBlock, ref shape);
					/*
					 * Optional stuff, need to check if we're running off the end of the stream before reading each block
					 */
					if (block.Length() - block.Position() > 1)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.subobject_shaders);
						ParseBlock(newBlock, ref shape);
					}

					if (block.Length() - block.Position() > 1)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.subobject_light_cfgs);
						ParseBlock(newBlock, ref shape);
					}

					if (block.Length() - block.Position() > 1)
					{
						int subObjectID = block.ReadInt32();
					}

					break;
				case KujuTokenID.subobject_light_cfgs:
					int[] subobject_light_cfgs = new int[block.ReadInt32()];
					for (int i = 0; i < subobject_light_cfgs.Length; i++)
					{
						subobject_light_cfgs[i] = block.ReadInt32();
					}

					break;
				case KujuTokenID.subobject_shaders:
					int[] subobject_shaders = new int[block.ReadInt32()];
					for (int i = 0; i < subobject_shaders.Length; i++)
					{
						subobject_shaders[i] = block.ReadInt32();
					}

					break;
				case KujuTokenID.vertices:
					int vertexCount = block.ReadInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (vertexCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.vertex);
						ParseBlock(newBlock, ref shape);
						vertexCount--;
					}

					break;
				case KujuTokenID.vertex:
					flags = block.ReadUInt32(); //Various control variables, not supported
					int myPoint = block.ReadInt32(); //Index to points array
					int myNormal = block.ReadInt32(); //Index to normals array

					Vertex v = new Vertex(shape.points[myPoint], shape.normals[myNormal]);

					uint Color1 = block.ReadUInt32();
					uint Color2 = block.ReadUInt32();
					newBlock = block.ReadSubBlock(KujuTokenID.vertex_uvs);
					ParseBlock(newBlock, ref shape, ref v);
					currentLOD.subObjects[currentLOD.subObjects.Count - 1].verticies.Add(v);
					break;
				case KujuTokenID.vertex_idxs:
					int remainingVertex = block.ReadInt32() / 3;
					while (remainingVertex > 0)
					{
						int v1 = block.ReadInt32();
						int v2 = block.ReadInt32();
						int v3 = block.ReadInt32();

						currentLOD.subObjects[currentLOD.subObjects.Count - 1].faces.Add(new Face(new[] { v1, v2, v3 }, currentLOD.subObjects[currentLOD.subObjects.Count - 1].materials.Count - 1));
						remainingVertex--;
					}

					break;
				case KujuTokenID.vertex_set:
					int vertexStateIndex = block.ReadInt32(); //Index to the vtx_states member
					int hierarchy = shape.vtx_states[vertexStateIndex].hierarchyID; //Now pull the hierachy ID out
					int setStartVertexIndex = block.ReadInt32(); //First vertex
					int setVertexCount = block.ReadInt32(); //Total number of vert
					VertexSet vts = new VertexSet();
					vts.hierarchyIndex = hierarchy;
					vts.startVertex = setStartVertexIndex;
					vts.numVerticies = setVertexCount;
					currentLOD.subObjects[currentLOD.subObjects.Count - 1].vertexSets.Add(vts);
					break;
				case KujuTokenID.vertex_sets:
					int vertexSetCount = block.ReadInt16();
					if (block is BinaryBlock)
					{
						block.ReadUInt16();
					}
					while (vertexSetCount > 0)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.vertex_set);
						ParseBlock(newBlock, ref shape);
						vertexSetCount--;
					}

					//We now need to transform our verticies
					currentLOD.subObjects[currentLOD.subObjects.Count - 1].TransformVerticies(shape);
					break;
				case KujuTokenID.vertex_uvs:
					int[] vertex_uvs = new int[block.ReadInt32()];
					for (int i = 0; i < vertex_uvs.Length; i++)
					{
						vertex_uvs[i] = block.ReadInt32();
					}

					//Looks as if vertex_uvs should always be of length 1, thus:
					vertex.TextureCoordinates = shape.uv_points[vertex_uvs[0]];
					break;

				/*
				 * ANIMATION CONTROLLERS AND RELATED STUFF
				 */

				case KujuTokenID.animations:
					int numAnimations = block.ReadInt32();
					for (int i = 0; i < numAnimations; i++)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.animation);
						ParseBlock(newBlock, ref shape);
					}
					break;
				case KujuTokenID.animation:
					Animation animation = new Animation
					{
						FrameCount = block.ReadInt32(),
						FrameRate = block.ReadInt32(),
						Nodes = new Dictionary<string, KeyframeAnimation>()
					};
					shape.Animations.Add(animation);
					newBlock = block.ReadSubBlock(KujuTokenID.anim_nodes);
					ParseBlock(newBlock, ref shape);
					break;
				case KujuTokenID.anim_nodes:
					int numNodes = block.ReadInt32();
					for (int i = 0; i < numNodes; i++)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.anim_node);
						ParseBlock(newBlock, ref shape);
					}
					break;
				case KujuTokenID.anim_node:
					Matrix4D matrix = Matrix4D.Identity;
					for (int i = 0; i < shape.Matricies.Count; i++)
					{
						if (shape.Matricies[i].Name == block.Label)
						{
							matrix = shape.Matricies[i].Matrix;
							break;
						}
					}
					KeyframeAnimation currentNode = new KeyframeAnimation(block.Label, shape.Animations[shape.Animations.Count - 1].FrameCount, shape.Animations[shape.Animations.Count - 1].FrameRate, matrix);
					newBlock = block.ReadSubBlock(KujuTokenID.controllers);
					ParseBlock(newBlock, ref shape, ref currentNode);
					if (currentNode.AnimationControllers.Length != 0)
					{
						newResult.Animations.Add(block.Label, currentNode);
					}
					break;
				case KujuTokenID.controllers:
					int numControllers = block.ReadInt32();
					animationNode.AnimationControllers = new AbstractAnimation[numControllers];
					for (int i = 0; i < numControllers; i++)
					{
						controllerIndex = i;
						newBlock = block.ReadSubBlock(new[] { KujuTokenID.tcb_rot, KujuTokenID.linear_pos });
						ParseBlock(newBlock, ref shape, ref animationNode);
					}
					break;
				case KujuTokenID.tcb_rot:
					NumFrames = block.ReadInt32();
					quaternionFrames = new QuaternionFrame[NumFrames];
					bool isSlerpRot = false;
					for (currentFrame = 0; currentFrame < NumFrames; currentFrame++)
					{
						newBlock = block.ReadSubBlock(new[] { KujuTokenID.tcb_key, KujuTokenID.slerp_rot });
						ParseBlock(newBlock, ref shape, ref animationNode);
						if (newBlock.Token == KujuTokenID.slerp_rot)
						{
							isSlerpRot = true;
						}
					}

					if (isSlerpRot)
					{
						animationNode.AnimationControllers[controllerIndex] = new SlerpRot(animationNode.Name, quaternionFrames);
					}
					else
					{
						animationNode.AnimationControllers[controllerIndex] = new TcbKey(animationNode.Name, quaternionFrames);
					}
					break;
				case KujuTokenID.tcb_key:
					// Frame index
					int frameIndex = block.ReadInt32();
					// n.b. we need to negate the Z and W components to get to GL format as opposed to DX
					Quaternion q = new Quaternion(block.ReadSingle(), block.ReadSingle(), -block.ReadSingle(), -block.ReadSingle());
					quaternionFrames[currentFrame] = new QuaternionFrame(frameIndex, q);
					/* 4 more floats:
					 * TENSION
					 * CONTINUITY
					 * IN
					 * OUT
					 */
					break;
				case KujuTokenID.slerp_rot:
					// Frame index
					frameIndex = block.ReadInt32();
					q = new Quaternion(block.ReadSingle(), block.ReadSingle(), -block.ReadSingle(), -block.ReadSingle());
					quaternionFrames[currentFrame] = new QuaternionFrame(frameIndex, q);
					break;
				case KujuTokenID.linear_pos:
					NumFrames = block.ReadInt32();
					vectorFrames = new VectorFrame[NumFrames];
					for (currentFrame = 0; currentFrame < NumFrames; currentFrame++)
					{
						newBlock = block.ReadSubBlock(KujuTokenID.linear_key);
						ParseBlock(newBlock, ref shape, ref animationNode);
					}
					animationNode.AnimationControllers[controllerIndex] = new LinearKey(animationNode.Name, vectorFrames);
					break;
				case KujuTokenID.linear_key:
					// Frame index
					frameIndex = block.ReadInt32();
					Vector3 translation = new Vector3(block.ReadSingle(), block.ReadSingle(), block.ReadSingle());
					vectorFrames[currentFrame] = new VectorFrame(frameIndex, translation);
					break;
			}
		}
	}
}
#pragma warning restore 0219
