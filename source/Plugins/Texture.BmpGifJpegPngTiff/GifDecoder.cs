﻿using System;
using System.Collections;
using System.Drawing;
using System.IO;
using OpenBveApi.Math;

namespace Plugin
{ 
	/*
	* No copyright asserted on the source code of this class.  May be used for
	* any purpose, however, refer to the Unisys LZW patent for any additional
	* restrictions.  Please forward any corrections to kweiner@fmsware.com.
	*
	* author Kevin Weiner, FM Software; LZW decoder adapted from John Cristy's ImageMagick.
	* https://imagemagick.org/script/license.php
	* version 1.03 November 2003
	*
	* Modifications & bugfixes for OpenBVE and general cleanup by Christopher Lees
	*/
	internal partial class GifDecoder 
	{
		protected Stream inStream;
		protected DecoderStatus status;

		protected int width; // full image width
		protected int height; // full image height
		protected bool gctFlag; // global color table used
		protected int gctSize; // size of global color table
		protected int loopCount = 1; // iterations; 0 = repeat forever

		protected int[] gct; // global color table
		protected int[] lct; // local color table
		protected int[] act; // active color table

		protected int bgIndex; // background color index
		protected int bgColor; // background color
		protected int lastBgColor; // previous bg color
		protected int pixelAspect; // pixel aspect ratio

		protected bool lctFlag; // local color table flag
		protected bool interlace; // interlace flag
		protected int lctSize; // local color table size

		protected int ix, iy, iw, ih; // current image rectangle
		protected Rectangle lastRect; // last image rect
		protected Image image; // current frame
		protected Bitmap bitmap;
		protected Image lastImage; // previous frame

		protected byte[] block = new byte[256]; // current data block
		protected int blockSize = 0; // block size

		// last graphic control extension info
		protected int dispose = 0;
		// 0=no action; 1=leave in place; 2=restore to bg; 3=restore to prev
		protected int lastDispose = 0;
		protected bool transparency = false; // use transparent color
		protected int delay = 0; // delay in milliseconds
		protected int transIndex; // transparent color index

		protected static readonly int MaxStackSize = 4096;
		// max decoder pixel stack size

		// LZW decoder working arrays
		protected short[] prefix;
		protected byte[] suffix;
		protected byte[] pixelStack;
		protected byte[] pixels;

		protected ArrayList frames; // frames read from current file
		protected int frameCount;

		public class GifFrame 
		{
			public GifFrame( Image im, int del) 
			{
				image = im;
				delay = del;
			}
			public Image image;
			public int delay;
		}

		
		/// <summary>Gets the duration of the specified frame</summary>
		/// <param name="n">The frame index</param>
		/// <returns>The frame duration in milliseconds</returns>
		public int GetDuration(int n) 
		{
			delay = -1;
			if ((n >= 0) && (n < frameCount)) 
			{
				delay = ((GifFrame) frames[n]).delay;
			}
			return delay;
		}

		/// <summary>Gets the total frame count</summary>
		public int GetFrameCount() 
		{
			return frameCount;
		}

		/// <summary>Creates the pixels for a new frame based upon a bitmap and previous frame data</summary>
		/// <param name="newBitmap">The bitmap</param>
		/// <returns>The new pixel array</returns>
		int [] GetPixels( Bitmap newBitmap)
		{
			int [] newPixels = new int [ 3 * image.Width * image.Height ];
			int count = 0;
			for (int th = 0; th < image.Height; th++)
			{
				for (int tw = 0; tw < image.Width; tw++)
				{
					Color color = newBitmap.GetPixel(tw, th);
					newPixels[count] = color.R;
					count++;
					newPixels[count] = color.G;
					count++;
					newPixels[count] = color.B;
					count++;
				}
			}
			return newPixels;
		}

		/// <summary>Sets the pixels for the current frame</summary>
		/// <param name="newPixels">The new pixel aray</param>
		void SetPixels(int[] newPixels)
		{
			int count = 0;
			for (int th = 0; th < image.Height; th++)
			{
				for (int tw = 0; tw < image.Width; tw++)
				{
					Color color = Color.FromArgb( newPixels[count++] );
					bitmap.SetPixel( tw, th, color );
				}
			}
		}

		/// <summary>Sets the pixels for a GIF frame from the current bitmap</summary>
		protected void SetPixels() 
		{
			int[] dest = GetPixels( bitmap );

			// fill in starting image contents based on last image's dispose code
			if (lastDispose > 0) 
			{
				if (lastDispose == 3)
				{
					// use image before last
					int n = frameCount - 2;
					lastImage = n > 0 ? GetFrame(n - 1) : null;
				}

				if (lastImage != null) 
				{
					int[] prev = GetPixels( new Bitmap( lastImage ) );
					Array.Copy(prev, 0, dest, 0, width * height);
					// copy pixels

					if (lastDispose == 2) 
					{
						// fill last image rect area with background color
						Graphics g = Graphics.FromImage( image );
						Color c;
						if (transparency) 
						{
							c = Color.FromArgb( 0, 0, 0, 0 ); 	// assume background is transparent
						} 
						else 
						{
							c = Color.FromArgb( lastBgColor ); // use given background color
						}
						Brush brush = new SolidBrush( c );
						g.FillRectangle( brush, lastRect );
						brush.Dispose();
						g.Dispose();
					}
				}
			}

			// copy each source line to the appropriate place in the destination
			int pass = 1;
			int inc = 8;
			int iline = 0;
			for (int i = 0; i < ih; i++) 
			{
				int line = i;
				if (interlace) 
				{
					if (iline >= ih) 
					{
						pass++;
						switch (pass) 
						{
							case 2 :
								iline = 4;
								break;
							case 3 :
								iline = 2;
								inc = 4;
								break;
							case 4 :
								iline = 1;
								inc = 2;
								break;
						}
					}
					line = iline;
					iline += inc;
				}
				line += iy;
				if (line < height) 
				{
					int k = line * width;
					int dx = k + ix; // start of line in dest
					int dlim = dx + iw; // end of dest line
					if ((k + width) < dlim) 
					{
						dlim = k + width; // past dest edge
					}
					int sx = i * iw; // start of line in source
					while (dx < dlim) 
					{
						// map color and insert in destination
						int index = pixels[sx++] & 0xff;
						int c = act[index];
						if (c != 0) 
						{
							dest[dx] = c;
						}
						dx++;
					}
				}
			}
			SetPixels( dest );
		}

		/// <summary>Gets an image containing the contents of the specified frame</summary>
		/// <param name="n">The frame number</param>
		/// <returns>The image</returns>
		public Image GetFrame(int n) 
		{
			Image im = null;
			if ((n >= 0) && (n < frameCount)) 
			{
				im = ((GifFrame) frames[n] ).image;
			}
			return im;
		}

		/// <summary>Gets the frame size</summary>
		/// <returns>A vector describing the size of each frame</returns>
		public Vector2 GetFrameSize() 
		{
			return new Vector2(width, height);
		}

		/// <summary>Reads a GIF file from the specified stream</summary>
		/// <param name="inputStream">The source stream</param>
		/// <returns>The status code</returns>
		public DecoderStatus Read( Stream inputStream ) 
		{
			Init();
			if ( inputStream != null) 
			{
				this.inStream = inputStream;
				ReadHeader();
				if (!Error()) 
				{
					ReadContents();
					if (frameCount < 0) 
					{
						status = DecoderStatus.FormatError;
					}
				}
				inputStream.Close();
			} 
			else 
			{
				status = DecoderStatus.OpenError;
			}
			return status;
		}

		/// <summary>Reads a GIF file from the specified source</summary>
		/// <param name="name">The source file / URL</param>
		/// <returns>The status code</returns>
		public DecoderStatus Read(String name) 
		{
			status = DecoderStatus.OK;
			try 
			{
				name = name.Trim().ToLower();
				status = Read( new FileInfo( name ).OpenRead() );
			} 
			catch (IOException) 
			{
				status = DecoderStatus.OpenError;
			}
			return status;
		}

		/// <summary>Decodes LZW image data into a pixel array</summary>
		/// <remarks>Adapted from ImageMagick code by John Cristy</remarks>
		protected void DecodeImageData() 
		{
			int NullCode = -1;
			int npix = iw * ih;
			int available, 
				clear,
				code_mask,
				code_size,
				end_of_information,
				in_code,
				old_code,
				bits,
				code,
				count,
				i,
				datum,
				data_size,
				first,
				top,
				bi,
				pi;

			if ((pixels == null) || (pixels.Length < npix)) 
			{
				pixels = new byte[npix]; // allocate new pixel array
			}
			if (prefix == null) prefix = new short[MaxStackSize];
			if (suffix == null) suffix = new byte[MaxStackSize];
			if (pixelStack == null) pixelStack = new byte[MaxStackSize + 1];

			//  Initialize GIF data stream decoder.

			data_size = Read();
			clear = 1 << data_size;
			end_of_information = clear + 1;
			available = clear + 2;
			old_code = NullCode;
			code_size = data_size + 1;
			code_mask = (1 << code_size) - 1;
			for (code = 0; code < clear; code++) 
			{
				prefix[code] = 0;
				suffix[code] = (byte) code;
			}

			//  Decode GIF pixel stream.

			datum = bits = count = first = top = pi = bi = 0;

			for (i = 0; i < npix;) 
			{
				if (top == 0) 
				{
					if (bits < code_size) 
					{
						//  Load bytes until there are enough bits for a code.
						if (count == 0) 
						{
							// Read a new data block.
							count = ReadBlock();
							if (count <= 0)
								break;
							bi = 0;
						}
						datum += (block[bi] & 0xff) << bits;
						bits += 8;
						bi++;
						count--;
						continue;
					}

					//  Get the next code.

					code = datum & code_mask;
					datum >>= code_size;
					bits -= code_size;

					//  Interpret the code

					if ((code > available) || (code == end_of_information))
						break;
					if (code == clear) 
					{
						//  Reset decoder.
						code_size = data_size + 1;
						code_mask = (1 << code_size) - 1;
						available = clear + 2;
						old_code = NullCode;
						continue;
					}
					if (old_code == NullCode) 
					{
						pixelStack[top++] = suffix[code];
						old_code = code;
						first = code;
						continue;
					}
					in_code = code;
					if (code == available) 
					{
						pixelStack[top++] = (byte) first;
						code = old_code;
					}
					while (code > clear) 
					{
						pixelStack[top++] = suffix[code];
						code = prefix[code];
					}
					first = suffix[code] & 0xff;

					//  Add a new string to the string table,

					if (available >= MaxStackSize)
						break;
					pixelStack[top++] = (byte) first;
					prefix[available] = (short) old_code;
					suffix[available] = (byte) first;
					available++;
					if (((available & code_mask) == 0)
						&& (available < MaxStackSize)) 
					{
						code_size++;
						code_mask += available;
					}
					old_code = in_code;
				}

				//  Pop a pixel off the pixel stack.

				top--;
				pixels[pi++] = pixelStack[top];
				i++;
			}

			for (i = pi; i < npix; i++) 
			{
				pixels[i] = 0; // clear missing pixels
			}

		}

		/// <summary>Returns whether an error was encountered whilst reading the GIF</summary>
		protected bool Error() 
		{
			return status != DecoderStatus.OK;
		}

		/// <summary>Initializes or reinitalizes the reader</summary>
		protected void Init() 
		{
			status = DecoderStatus.OK;
			frameCount = 0;
			frames = new ArrayList();
			gct = null;
			lct = null;
		}

		/// <summary>Reads a single byte from the input stream</summary>
		protected int Read() 
		{
			int curByte = 0;
			try 
			{
				curByte = inStream.ReadByte();
			} 
			catch (IOException) 
			{
				status = DecoderStatus.FormatError;
			}
			return curByte;
		}

		/// <summary>Reads the next variable length input block</summary>
		/// <returns>The remaining input bytes stored in the buffer</returns>
		protected int ReadBlock() 
		{
			blockSize = Read();
			int n = 0;
			if (blockSize > 0) 
			{
				try 
				{
					while (n < blockSize) 
					{
						int count = inStream.Read(block, n, blockSize - n);
						if (count == -1) 
							break;
						n += count;
					}
				} 
				catch (IOException) 
				{
				}

				if (n < blockSize) 
				{
					status = DecoderStatus.FormatError;
				}
			}
			return n;
		}

		/// <summary>Reads the GIF Color Table as 256 integer values</summary>
		/// <param name="ncolors">The number of colors to read</param>
		/// <returns>The GIF color table</returns>
		protected int[] ReadColorTable(int ncolors) 
		{
			int nbytes = 3 * ncolors;
			int[] tab = null;
			byte[] c = new byte[nbytes];
			int n = 0;
			try 
			{
				n = inStream.Read(c, 0, c.Length );
			} 
			catch (IOException) 
			{
			}
			if (n < nbytes) 
			{
				status = DecoderStatus.FormatError;
			} 
			else 
			{
				tab = new int[256]; // max size to avoid bounds checks
				int i = 0;
				int j = 0;
				while (i < ncolors) 
				{
					byte r = (byte) (c[j++] & 0xff);
					byte g = (byte) (c[j++] & 0xff);
					byte b = (byte) (c[j++] & 0xff);
					tab[i++] = BitConverter.ToInt32(new[] {b,g,r,byte.MaxValue}, 0);
				}
			}
			return tab;
		}

		/// <summary>Reads the contents of the file</summary>
		protected void ReadContents() 
		{
			// read GIF file content blocks
			bool done = false;
			while (!(done || Error())) 
			{
				int code = Read();
				switch (code) 
				{
					case 0x2C : // image separator
						ReadNextFrame();
						break;
					case 0x21 : // extension
						code = Read();
						switch (code)
						{
							case 0xf9: // graphics control extension
								ReadGraphicControlExt();
								break;
							case 0xff: // application extension
								ReadBlock();
								string app = "";
								for (int i = 0; i < 11; i++)
								{
									app += (char) block[i];
								}

								if (app.Equals("NETSCAPE2.0"))
								{
									ReadNetscapeExt();
								}
								else
								{
									Skip(); // don't care
								}
								break;
							default: // uninteresting extension
								Skip();
								break;
						}
						break;
					case 0x3b : // terminator
						done = true;
						break;
						case 0x00 : // bad byte, but keep going and see what happens
						break;
					default :
						status = DecoderStatus.FormatError;
						break;
				}
			}
		}

		/// <summary>Reads the Graphic Control Extension values</summary>
		protected void ReadGraphicControlExt() 
		{
			Read(); // block size
			int packed = Read(); // packed fields
			dispose = (packed & 0x1c) >> 2; // disposal method
			if (dispose == 0) 
			{
				dispose = 1; // elect to keep old image if discretionary
			}
			transparency = (packed & 1) != 0;
			delay = ReadShort() * 10; // delay in milliseconds
			transIndex = Read(); // transparent color index
			Read(); // block terminator
		}

		/// <summary>Reads the GIF header</summary>
		protected void ReadHeader() 
		{
			String id = "";
			for (int i = 0; i < 6; i++) 
			{
				id += (char) Read();
			}
			if (!id.StartsWith("GIF")) 
			{
				status = DecoderStatus.FormatError;
				return;
			}

			ReadLSD();
			if (gctFlag && !Error()) 
			{
				gct = ReadColorTable(gctSize);
				bgColor = gct[bgIndex];
			}
		}

		/// <summary>Reads the next frame image</summary>
		protected void ReadNextFrame() 
		{
			ix = ReadShort(); // (sub)image position & size
			iy = ReadShort();
			iw = ReadShort();
			ih = ReadShort();

			int packed = Read();
			lctFlag = (packed & 0x80) != 0; // 1 - local color table flag
			interlace = (packed & 0x40) != 0; // 2 - interlace flag
			// 3 - sort flag
			// 4-5 - reserved
			lctSize = 2 << (packed & 7); // 6-8 - local color table size

			if (lctFlag) 
			{

				lct = ReadColorTable(lctSize); // read table
				act = lct; // make local table active
			} 
			else 
			{
				act = gct; // make global table active
				if (bgIndex == transIndex)
					bgColor = 0;
			}
			int save = 0;
			
			if (transparency) 
			{
				save = act[transIndex];
				act[transIndex] = 0; // set transparent color if specified
			}

			if (act == null) 
			{
				status = DecoderStatus.FormatError; // no color table defined
			}

			if (Error()) return;

			DecodeImageData(); // decode pixel data
			Skip();

			if (Error()) return;

			frameCount++;

			// create new image to receive frame data
			bitmap = new Bitmap(width, height);
			image = bitmap;
			SetPixels(); // transfer pixel data to image

			frames.Add(new GifFrame(bitmap, delay)); // add image to frame list

			if (transparency) 
			{
				act[transIndex] = save;
			}
			ResetFrame();

		}

		/// <summary>Reads the logical screen descriptor</summary>
		protected void ReadLSD() 
		{

			// logical screen size
			width = ReadShort();
			height = ReadShort();

			// packed fields
			int packed = Read();
			gctFlag = (packed & 0x80) != 0; // 1   : global color table flag
			// 2-4 : color resolution
			// 5   : gct sort flag
			gctSize = 2 << (packed & 7); // 6-8 : gct size

			bgIndex = Read(); // background color index
			pixelAspect = Read(); // pixel aspect ratio
		}

		/// <summary>Reads the Netscape extensions header</summary>
		/// <remarks>This sets the itineration count</remarks>
		protected void ReadNetscapeExt() 
		{
			do 
			{
				ReadBlock();
				if (block[0] == 1) 
				{
					// loop count sub-block
					int b1 = block[1] & 0xff;
					int b2 = block[2] & 0xff;
					loopCount = (b2 << 8) | b1;
				}
			} while ((blockSize > 0) && !Error());
		}

		/// <summary>Reads the next 16-bit value, LSB first</summary>
		protected int ReadShort() 
		{
			// read 16-bit value, LSB first
			return Read() | (Read() << 8);
		}

		/// <summary>Resets the frame state before reading the next image</summary>
		protected void ResetFrame() 
		{
			lastDispose = dispose;
			lastRect = new Rectangle(ix, iy, iw, ih);
			lastImage = image;
			lastBgColor = bgColor;
			transparency = false;
			delay = 0;
			lct = null;
		}

		/// <summary>Skips variable length blocks upto and including the next zero length block</summary>
		protected void Skip() 
		{
			do 
			{
				ReadBlock();
			} while ((blockSize > 0) && !Error());
		}
	}
}

