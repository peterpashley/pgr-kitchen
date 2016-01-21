using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class DrawingView : MonoBehaviour 
{
	public bool updateView;
	public MeshRenderer linePrefab;
	public Material blackLine;
	public Material greyLine;

	// Use this for initialization
	void Start () 
	{
	
	}
	
	// Update is called once per frame
	void Update () 
	{
		if( updateView )
		{
			updateView = false;
			Camera cam = this.GetComponent<Camera>();
			cam.aspect = 1f;

			float pixelSize = 2f*cam.orthographicSize / (float)Screen.width;

			while( transform.childCount > 0 )
			{
				Transform child = transform.GetChild(0);
				child.parent = null;
				GameObject.DestroyImmediate( child.gameObject );
			}

			var cubes = GameObject.FindObjectsOfType<LayoutCuboid>();
			foreach( var cube in cubes )
			{
				float lineWidth = pixelSize * (float)cube.edgeLineSize;
				for( int i=0 ; i<4 ; i++ )
				{
					var renderer = GameObject.Instantiate(linePrefab);
					Vector3 offset = cube.size;
					if( cube.edgeLineStrength < 0.75f )
					{
						renderer.sharedMaterial = greyLine;
					}
					else
					{
						renderer.sharedMaterial = blackLine;
					}

					offset.y = 0f;

					switch( i )
					{
					case 0:
					case 2:
						offset.z = 0f;
						break;
					case 1:
					case 3:
						offset.x = 0f;
						break;
					}
					switch( i )
					{
					case 0:
					case 1:
						offset *= 1f;
						break;
					case 2:
					case 3:
						offset *= -1f;
						break;
					}
					offset *= 0.5f;
					float angle = 90f * i;
					Vector3 pos = cube.transform.position + offset;

					// Align to pixels:
					Vector3 posScreen = cam.WorldToScreenPoint( pos );
					posScreen.x = Mathf.Round(posScreen.x);
					posScreen.y = Mathf.Round(posScreen.y);
					pos = cam.ScreenToWorldPoint( posScreen );
					//pos.x = lineWidth*Mathf.Round(pos.x/lineWidth+0.5f);
					//pos.z = lineWidth*Mathf.Round(pos.z/lineWidth+0.5f);
					renderer.transform.position = pos;
					renderer.transform.rotation = Quaternion.AngleAxis( angle, Vector3.up );
					renderer.transform.parent = this.transform;
					Vector3 scale = renderer.transform.localScale;
					scale.x = lineWidth;
					scale.z = 2f*Mathf.Max(0.5f*cube.size.x - Mathf.Abs(offset.x), 0.5f*cube.size.z - Mathf.Abs(offset.z) );
					renderer.transform.localScale = scale;
				}
			}
		}
	}
}
