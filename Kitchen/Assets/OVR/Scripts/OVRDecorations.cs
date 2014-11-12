/************************************************************************************

Filename    :   OVREyeDecorations.cs
Content     :   Implements the eye decorations
Created     :   July 4, 2014
Authors     :   G

Copyright   :   Copyright 2014 Oculus VR, Inc. All Rights reserved.

Licensed under the Oculus VR Rift SDK License Version 3.1 (the "License"); 
you may not use the Oculus VR Rift SDK except in compliance with the License, 
which is provided at the time of installation or download, or which 
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

http://www.oculusvr.com/licenses/LICENSE-3.1 

Unless required by applicable law or agreed to in writing, the Oculus VR SDK 
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/
using UnityEngine;
using System;

/// <summary>
/// OVR Post Render effects: vignette
/// </summary>
public class OVREyeDecorations
{	
	
	#region Member Variables
	// PRIVATE MEMBERS	

	private Mesh 		vignetteMesh = null;
	
	private Vector3[] 	positions = null;
	private Color32[]	colors = null;

	private int[] 		indices = null;

	private Material 	material = null;

	private float 		xFraction = 128.0f / 1024.0f;
	private float		yFraction = 128.0f / 1024.0f;

	#endregion		
		
	/// <summary>
	/// Generates the vignette geometry.
	/// </summary>
	public void BuildVignette()
	{
		if (vignetteMesh != null)
		{
			Debug.Log( "Vignette mesh already created" );
			return;
		}

		vignetteMesh = new Mesh ();
		vignetteMesh.Clear ();

		const int gridCountX = 6;
		const int gridCountY = 6;

		// Leave 25% of the vignette as solid black
		float[] posx = new float[gridCountX] { -1.001f, -1.0f + xFraction * 0.25f, -1.0f + xFraction, 1.0f - xFraction, 1.0f - xFraction * 0.25f, 1.001f };
		float[] posy = new float[gridCountY] { -1.001f, -1.0f + yFraction * 0.25f, -1.0f + yFraction, 1.0f - yFraction, 1.0f - yFraction * 0.25f, 1.001f };

		int vertexCount = gridCountX * gridCountY;
	
		positions = new Vector3[vertexCount];
		colors = new Color32[vertexCount];

		// To guarantee that the edge pixels are completely black, we need to
		// have a band of solid 0.  Just interpolating to 0 at the edges will
		// leave some pixels with low color values.  This stuck out as surprisingly
		// visible smears from the distorted edges of the eye renderings in
		// some cases.
		for ( int y = 0; y < 6; y++ )
		{
			for ( int x = 0; x < 6; x++ )
			{
				int idx = y * 6 + x;
				positions[idx].x = posx[x];
				positions[idx].y = posy[y];
				positions[idx].z = 0.0f;
				// the outer edges will have 0 color
				byte c = ( y <= 1 || y >= 4 || x <= 1 || x >= 4 ) ? (byte)0 : (byte)255;
				colors[idx].r = c;
				colors[idx].g = c;
				colors[idx].b = c;
				colors[idx].a = 255;
			}
		}

		int indexCount = 24 * 6;

		indices = new int[indexCount];
		
		int index = 0;
		for ( int x = 0; x < 5; x++ )
		{
			for ( int y = 0; y < 5; y++ )
			{
				if ( x == 2 && y == 2 )
				{
					continue;	// the middle is open
				}
				// flip triangulation at corners
				if ( x == y )
				{
					indices[index + 0] = y * 6 + x;
					indices[index + 1] = (y + 1) * 6 + x + 1;
					indices[index + 2] = (y + 1) * 6 + x;
					indices[index + 3] = y * 6 + x;
					indices[index + 4] = y * 6 + x + 1;
					indices[index + 5] = (y + 1) * 6 + x + 1;
				}
				else
				{
					indices[index + 0] = y * 6 + x;
					indices[index + 1] = y * 6 + x + 1;
					indices[index + 2] = (y + 1) * 6 + x;
					indices[index + 3] = (y + 1) * 6 + x;
					indices[index + 4] = y * 6 + x + 1;
					indices[index + 5] = (y + 1) * 6 + x + 1;
				}
				index += 6;
			}
		}

		vignetteMesh.vertices  = positions;
		vignetteMesh.colors32  = colors;
		vignetteMesh.triangles = indices;

		if (material == null) {
			material = new Material ( Shader.Find( "Custom/OVREyeDecoration_Mesh" ) );
		}
	}

	/// <summary>
	/// Draw eye decorations post distorted
	/// </summary>
	public void Draw()
	{
		// Draw a thin vignette at the edges of the view so clamping will give black
		if(vignetteMesh != null)
		{
			GL.PushMatrix ();
			GL.LoadOrtho ();
			for(int i = 0; i < material.passCount; i++)
			{
				material.SetPass(i);
				Graphics.DrawMeshNow(vignetteMesh, Matrix4x4.identity);
			}
			GL.PopMatrix ();
		}
	}

}
