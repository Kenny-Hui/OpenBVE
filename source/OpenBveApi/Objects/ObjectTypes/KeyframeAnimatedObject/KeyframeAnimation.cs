﻿//Simplified BSD License (BSD-2-Clause)
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


using OpenBveApi.Math;
using OpenBveApi.Trains;

namespace OpenBveApi.Objects
{
	/// <summary>An animation using keyframes</summary>
    public class KeyframeAnimation
    {
		/// <summary>The animation name</summary>
	    public readonly string Name;
		/// <summary>The base matrix before transforms are performed</summary>
	    private readonly Matrix4D baseMatrix;

		/*
	     * FRAMERATE
		 * ---------
		 *
		 * FrameRate indicates the number of frames per second (of real-time) the animation advances.
		 * 
		 * WHEELS
		 * ------
		 *
		 * An animation with the name prefixed with WHEELS reads the corresponding wheel radius from the ENG file and uses a pretty standard odometer to rotate
		 * It has 9 frames for a total of 1 revolution.
		 *
		 * We need to ignore (!) the time advance, and instead use the total wheel revolution.
		 * The ROD prefixed animations will likewise require linking to the WHEEL position.
		 * Essentially, read back through the hierarchy tree for the wheel pos. (Will require a new animation subtype)
		 *
		 * For the minute however, let's just try and animate everything at the FPS value.
		 * This will give us constantly rotating rods, wheels etc and proves the point (!)
		 *
	     * MSTS bug / feature
	     * ------------------
	     * The FrameRate number is divided by 30fps for interior views
		 * ref http://www.elvastower.com/forums/index.php?/topic/29692-animations-in-the-passenger-view-too-fast/page__p__213634
	     *
	     */

		/// <summary>The framerate</summary>
		public readonly double FrameRate;

	    /// <summary>The total number of frames in the animation</summary>
	    public readonly int FrameCount;


	    /// <summary>The controllers for the animation</summary>
	    public AbstractAnimation[] AnimationControllers;
		/// <summary>The matrix to send to the shader</summary>
	    public Matrix4D Matrix;
		/// <summary>The current animation key</summary>
	    internal double AnimationKey;

	    /// <summary>Creates a new keyframe animation</summary>
	    /// <param name="name">The animation name</param>
	    /// <param name="frameCount">The total number of frames in the animation</param>
	    /// <param name="frameRate">The framerate of the animation</param>
	    /// <param name="matrix">The base matrix to be transformed</param>
	    public KeyframeAnimation(string name, int frameCount, double frameRate, Matrix4D matrix)
	    {
			Name = name;
		    baseMatrix = matrix;
			FrameCount = frameCount;
			FrameRate = frameRate / 100;
	    }

	    /// <summary>Updates the animation</summary>
		public void Update(AbstractTrain train, int carIndex, Vector3 position, double trackPosition, int sectionIndex, bool isPartOfTrain, double timeElapsed)
		{
			// calculate the current keyframe for the animation
			AnimationKey += timeElapsed * FrameRate;
			AnimationKey %= FrameCount;
			// we start off with the base matrix (clone!)
			Matrix = new Matrix4D(baseMatrix);
			for (int i = 0; i < AnimationControllers.Length; i++)
			{
				if (AnimationControllers[i] != null)
				{
					// for each of the valid controllers within the animation, perform an update
					AnimationControllers[i].Update(AnimationKey, timeElapsed, ref Matrix);
				}
				
			}
		}

	}
}
