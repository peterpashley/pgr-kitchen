using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RulerWindow : EditorWindow 
{
	// Add menu named "My Window" to the Window menu
	[MenuItem ("Window/Pash/RulerWindow")]
	static void Init () {
		// Get existing open window or if none, make a new one:
		
		EditorWindow.GetWindow (typeof (RulerWindow));
	}

	bool _enableBox;

	RulerWindow()
	{
		SceneView.onSceneGUIDelegate += OnSceneGUI;
	}

	void Update()
	{
		Repaint();
	}

	void OnFocus()
	{
		SceneView.onSceneGUIDelegate -= OnSceneGUI;
		SceneView.onSceneGUIDelegate += OnSceneGUI;
	}

	void OnDestroy()
	{
		SceneView.onSceneGUIDelegate -= OnSceneGUI;
	}
	
	void OnGUI () 
	{
		GameObject obj = Selection.activeGameObject;
		if( obj && obj.GetComponent<Renderer>() )
		{
			Bounds bounds = obj.GetComponent<Renderer>().bounds;
			GUILayout.Label(obj.name + " size:" + (bounds.max - bounds.min) );


		}

	}
	
	
	
	void OnSceneGUI(SceneView sceneView)
	{
		GameObject obj = Selection.activeGameObject;
		if( obj && obj.GetComponent<Renderer>() )
		{
			Handles.color = Color.white;
			Bounds bounds = obj.GetComponent<Renderer>().bounds;
			DrawLabel( bounds.center, bounds.max, 0 );
			DrawLabel( bounds.center, bounds.max, 1 );
			DrawLabel( bounds.center, bounds.max, 2 );
			DrawLabel( bounds.center, bounds.min, 0 );
			DrawLabel( bounds.center, bounds.min, 1 );
			DrawLabel( bounds.center, bounds.min, 2 );
		}
	}

	void DrawLabel( Vector3 center, Vector3 extent, int axis )
	{
		Vector3 pos = center;
		pos[axis] = extent[axis];
		Handles.Label( pos, "" + extent[axis] );
	}

	
	
}
