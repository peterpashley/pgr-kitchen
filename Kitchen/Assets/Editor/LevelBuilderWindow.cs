using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

//NOTE: LevelBuilderFunctions is defined in its own file (not part of editor)
public class LevelBuilderWindow : EditorWindow 
{
	LevelBuilderFunctions _BuilderFunctions = new LevelBuilderFunctions();

	//float		_snapDistance= 0.001f;
	//bool 		_excludeWhiteBox=true; 
	//bool 		_fullLightBake=true;
	bool 		_enableBuildSelected=false;

	//Dictionary<LevelName, bool> _buildList = new Dictionary<LevelName, bool>();

	/*
	bool 		_generatingLightMapsInProgress = false;
	GameObject	_newlightmapExportGO;

	string		_saveSceneName;


	List<string>	_buildQueue = new List<string>();

	static Dictionary< Mesh, float > _meshScaleMap = new Dictionary<Mesh, float>();

	enum LogMode
	{
		Warning,
		Scenes,
		Objects,
		Meshes,
	}

	static LogMode _logMode = LogMode.Scenes;

	static void Log( LogMode mode, string text, GameObject obj = null )
	{
		if( (int)_logMode >= (int)mode )
		{
			if( mode == LogMode.Warning )
			{
				Debug.LogWarning( text, obj );
			}
			else
			{
				Debug.Log( text, obj );
			}
		}
	}
*/
	// Add menu named "My Window" to the Window menu
	[MenuItem ("ustwo/LevelBuilderWindow")]
	static void Init () {
		// Get existing open window or if none, make a new one:

		EditorWindow.GetWindow (typeof (LevelBuilderWindow));
	}
	
	void OnGUI () 
	{
		//TestCoords();

		#region Cancel bake
		//cancel bake light map 
		if( _BuilderFunctions._generatingLightMapsInProgress )
		{
			GUILayout.Label ("Baking Lightmap for " + EditorApplication.currentScene, EditorStyles.boldLabel);

			if(GUILayout.Button("CANCEL BAKE"))
			{
				Lightmapping.Cancel();
				_BuilderFunctions._generatingLightMapsInProgress = false;
				_BuilderFunctions.UndoLightMapOffsetPosition();
			}
			return;
		}
		#endregion
		bool exportable = !EditorApplication.currentScene.Contains( "Export" );

		#region Lightmaps
		{
			GUILayout.Label ("Lightmap", EditorStyles.boldLabel);

			GUI.enabled = !_BuilderFunctions._generatingLightMapsInProgress;

			GUI.enabled = exportable;
			if(GUILayout.Button("Build Light Map for current level"))
			{
				_BuilderFunctions.DoBuildLightingOnly();
			}
			GUI.enabled = true;

		}
		#endregion

		#region Build Settings
		{
			
			GUILayout.Label ("Level Export Settings", EditorStyles.boldLabel); 
			//_BuilderFunctions._excludeWhiteBox = EditorGUILayout.Toggle ("Exclude WhiteBox", _BuilderFunctions._excludeWhiteBox);		
			_BuilderFunctions._fullLightBake = false;//EditorGUILayout.Toggle ("also do offset and light bake", _BuilderFunctions._fullLightBake);
			//_snapDistance = EditorGUILayout.FloatField ("snap distance", _snapDistance);
		
			if(GUILayout.Button("Build Physics"))
			{
				_BuilderFunctions.DeleteExistingBuiltPhysics();
				_BuilderFunctions.BuildPhysicsData();
			}
			if(GUILayout.Button("Build Reflection Geometry"))
			{
				//makes a copy of every renderer and then changes the y scale and the layer

				_BuilderFunctions.DeleteExistingBuiltReflections();
				_BuilderFunctions.BuildReflectionData();
			}
			if(GUILayout.Button("Build Water Ripple Geometry"))
			{
				//makes a copy of every renderer and then changes the y scale and the layer
				
				_BuilderFunctions.DeleteExistingBuiltRippleData();
				_BuilderFunctions.BuildRippleData();
			}
			if(GUILayout.Button("Combine Move blocker Geometry"))
			{
				//makes a copy of every renderer and then changes the y scale and the layer
				
				_BuilderFunctions.DeleteExistingBuiltMoveBlockers();
				_BuilderFunctions.CombineMoveBlockers();
			}
			if(GUILayout.Button("combine all MeshCombineTags"))
			{
				//makes a copy of every renderer and then changes the y scale and the layer
				_BuilderFunctions.CombineAllTags();

			}
			if(GUILayout.Button("Clear All Generated Data"))
			{
				_BuilderFunctions.ClearAllGenerated();
			}
			if(GUILayout.Button("show all source data"))
			{
				_BuilderFunctions.ShowAllCombinedSource();
			}
			if(GUILayout.Button("show all combined data"))
			{
				_BuilderFunctions.ShowAllCombined();
			}
			GUI.enabled = !_BuilderFunctions._generatingLightMapsInProgress && exportable;
			if(GUILayout.Button("Export current level"))
			{
				_BuilderFunctions.DoExportCurrent ();
			}

			GUI.enabled = true;
		}
		#endregion

		/*
		#region Build All
		_enableBuildSelected = EditorGUILayout.Toggle ("Enable Build Selected Button", _enableBuildSelected);
		if (_enableBuildSelected)
		{
			LevelName[] values = (LevelName[])System.Enum.GetValues(typeof(LevelName));
			
			int numLevels = values.Length;
			
			if( _BuilderFunctions._buildList.Count != numLevels )
			{
				foreach( LevelName level in values )
				{
					if( !_BuilderFunctions._buildList.ContainsKey(level) )
					{
						_BuilderFunctions._buildList.Add(level, false);
					}
				}
			}
			
			foreach( LevelName level in values )
			{
				if( level == LevelName.None )
				{
					continue;
				}
				GUI.enabled = LevelManager.Instance.GetSceneName( level ).Contains( " Export" );
				_BuilderFunctions._buildList[level] = EditorGUILayout.Toggle (LevelManager.Instance.GetLevelName(level), _BuilderFunctions._buildList[level]);
			}
			GUI.enabled = true;
			//buildList
				
			GUI.enabled = !_BuilderFunctions._generatingLightMapsInProgress;
			if(GUILayout.Button("BULD SELECTED LEVELS!"))
			{
				_BuilderFunctions.DoExportSelected ();
				
			}
			
			GUI.enabled = true;
		}
		#endregion

		#region Build all
		{
			GUI.enabled = !_BuilderFunctions._generatingLightMapsInProgress;
			if(GUILayout.Button("Export and Test ALL LEVELS"))
			{
				_BuilderFunctions.DoExportAndTestAll();
			}
			GUI.enabled = true;
		}
		#endregion
		*/

		
	}



	void Update()
	{
		//_BuilderFunctions.Update();

		if( _BuilderFunctions._generatingLightMapsInProgress )
		{
			//finished running?
			if(!Lightmapping.isRunning)
			{
				OnDidCompleteLightMapBake();
			}
		}
	}

	void OnDidCompleteLightMapBake ()
	{
		LevelBuilderFunctions.Log (LevelBuilderFunctions.LogMode.Scenes, "Generating Light Map Done");
		_BuilderFunctions._generatingLightMapsInProgress = false;
		_BuilderFunctions.UndoLightMapOffsetPosition ();
		bool builtOne = false;
		if (_BuilderFunctions._buildQueue.Count > 0) 
		{
			builtOne = true;
			EditorApplication.SaveScene (_BuilderFunctions._saveSceneName);
			EditorApplication.OpenScene (EditorApplication.currentScene);
			_BuilderFunctions._buildQueue.RemoveAt (0);
		}

		if (_BuilderFunctions._buildQueue.Count > 0) 
		{
			_BuilderFunctions.LoadAndExportScene (_BuilderFunctions._buildQueue [0]);
		}
		else 
		{
			if (builtOne) 
			{
				LevelBuilderFunctions.Log (LevelBuilderFunctions.LogMode.Scenes, "combining meshes complete");
			}
			else 
			{
				LevelBuilderFunctions.Log (LevelBuilderFunctions.LogMode.Scenes, "light map complete");
			}
			Repaint ();

			if( _BuilderFunctions._testWhenExportCompleted )
			{
			}
		}
	}	

	

}
