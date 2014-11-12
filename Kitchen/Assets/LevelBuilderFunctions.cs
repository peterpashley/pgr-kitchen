using UnityEngine;
using System.Collections.Generic; 
#if UNITY_EDITOR
using UnityEditor;
public class LevelBuilderFunctions
{
	float		_snapDistance= 0.001f;
	public bool 		_excludeWhiteBox=false;
	public bool 		_fullLightBake=true;
	//public bool 		_enableBuildSelected=false;
	
	public bool 		_generatingLightMapsInProgress = false;
	GameObject	_newlightmapExportGO;
	
	public string		_saveSceneName;

	public enum LevelName 
	{
		None,
		Dummy,
	}
	
	public Dictionary<LevelName, bool> _buildList = new Dictionary<LevelName, bool>();
	
	public List<string>	_buildQueue = new List<string>();
	
	static Dictionary< Mesh, float > _meshScaleMap = new Dictionary<Mesh, float>();
	
	public enum LogMode
	{
		Warning,
		Scenes,
		Objects,
		Meshes,
	}
	
	static public LogMode _logMode = LogMode.Scenes;
	
	static public  void Log( LogMode mode, string text, GameObject obj = null )
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

	public void DoExportCurrent ()
	{
		_meshScaleMap.Clear();
		_buildQueue.Clear ();
		_buildQueue.Add (EditorApplication.currentScene);
		ExportCurrentScene ();
	}
	
	public void DoExportSelected ()
	{
		_buildQueue.Clear ();
		foreach (LevelName levelID in _buildList.Keys) 
		{
			if (_buildList [levelID]) 
			{
				string path = "Assets/Scenes/Final/" + "BLAH" + ".unity";
				_buildQueue.Add (path);
			}
		}
		LoadAndExportScene (_buildQueue [0]);
	}
	
	public bool _testWhenExportCompleted = false;
	
	public void DoExportAndTestAll ()
	{
		LevelName[] values = (LevelName[])System.Enum.GetValues(typeof(LevelName));
		
		// Enable all levels
		foreach( LevelName level in values )
		{
			if( !_buildList.ContainsKey(level) )
			{
				_buildList.Add(level, false);
			}
			if( level != LevelName.None )
			{
				_buildList[level] = true;
			}
		}
		
		DoExportSelected();
		
		_testWhenExportCompleted = true;
	}
	
	public void Update()
	{
		if( _generatingLightMapsInProgress )
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
		//TODO, ensure this is connetected correctly!!
		Log (LogMode.Scenes, "Generating Light Map Done");
		_generatingLightMapsInProgress = false;
		UndoLightMapOffsetPosition ();
		bool builtOne = false;
		if (_buildQueue.Count > 0) 
		{
			builtOne = true;
			EditorApplication.SaveScene (_saveSceneName);
			EditorApplication.OpenScene (EditorApplication.currentScene);
			_buildQueue.RemoveAt (0);
		}
		
		if (_buildQueue.Count > 0) 
		{
			LoadAndExportScene (_buildQueue [0]);
		}
		else 
		{
			if (builtOne) 
			{
				Log (LogMode.Scenes, "combining meshes complete");
			}
			else 
			{
				Log (LogMode.Scenes, "light map complete");
			}
			//Repaint ();
			
			if( _testWhenExportCompleted )
			{
				// Start tests here!
				//NOTE, I broke this!
				//LevelTestTool.ShowWindow();
				//LevelTestTool.TestAllLevels();
			}
		}
	}	
	
	public void DoBuildLightingOnly()
	{
		_newlightmapExportGO=new GameObject("LightMap");
		_newlightmapExportGO.AddComponent<LightMapOffset>();
		LightMapOffset lightMapUtil=_newlightmapExportGO.GetComponent<LightMapOffset>();
		
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		foreach (GameObject gameObject in allGameObjects)
		{
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			
			if (meshTag!=null && meshTag.staticLightMapOffset)
			{
				StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags( gameObject );
				if( meshTag.staticLightMap )
				{
					staticFlags |= StaticEditorFlags.LightmapStatic;
				}
				else
				{
					staticFlags &= ~StaticEditorFlags.LightmapStatic;
				}
				if( meshTag.staticBatch )
				{
					staticFlags |= StaticEditorFlags.BatchingStatic;
				}
				else
				{
					staticFlags &= ~StaticEditorFlags.BatchingStatic;
				}
				
				lightMapUtil.gameObjectsArray.Add( gameObject );
				
				// PBPTODO: Don't think this is needed except during an export!
				if (meshTag.staticLightMap)
				{
					GameObjectUtility.SetStaticEditorFlags(gameObject,staticFlags);
					
					//flag all children too
					MeshFilter[] childMeshCandidates=gameObject.GetComponentsInChildren<MeshFilter>();
					
					foreach(MeshFilter candidateMesh in childMeshCandidates)
					{
						if (gameObject.layer!=LayerMask.NameToLayer("WhiteBox"))
						{
							if (CheckValidToCombine(candidateMesh,gameObject,0))
							{
								GameObjectUtility.SetStaticEditorFlags(candidateMesh.gameObject,staticFlags);						
							}
						}
					}
				}				
			}							
		}
		
		OffsetMeshesAndStartBakeLightMap();
	}
	
	public void LoadAndExportScene( string path )
	{
		_saveSceneName = "";
		Log( LogMode.Scenes, "building:"+path);
		bool openSuccess = EditorApplication.OpenScene(path);
		
		if(openSuccess)
		{
			ExportCurrentScene();
		}
		else
		{
			Debug.LogError( "Failed to OpenScene " + path );
		}
	}
	//----
	void StripWhiteBoxAndCull()
	{
		//logic works like this:
		//look through all game objects
		//if on whitebox layer
		//delete and meshrender compenents that are desabled
		//if an object has no children and nothing other than a mesh filter on it, delete it
		//repeat until non are found to delete
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		//use this code to collapse hierarchy
		//for (int itter = 0 ;itter <5; itter++)
		{
			foreach (GameObject gameObject in allGameObjects)
			{
				if (gameObject.layer==LayerMask.NameToLayer("WhiteBox"))
				{
					if (gameObject.transform.childCount==0)
					{
						bool meshOrRender = false;
						bool hasAnyThingOtherThanMesh = false;
						foreach(var component in gameObject.GetComponents<Component>())
						{

							if (component is MeshFilter)
							{
								meshOrRender = true;
							}
							else if (component is MeshRenderer)
							{
								meshOrRender =true;
							}
							else if (!(component is Transform))
							{
								hasAnyThingOtherThanMesh =true;
							}

						}
						if (hasAnyThingOtherThanMesh)
						{
							//do nothing
							continue;
						}
						else
						{								
							if (meshOrRender && gameObject.GetComponent<MeshRenderer>())
							{
								if (gameObject.GetComponent<MeshRenderer>().enabled == false)
								{
									Debug.Log("destroying object!");
									GameObject.DestroyImmediate(gameObject);
								}
							}
							else
							{
								// for now we do nothing in this case (just to be safe)
								//GameObject.DestroyImmediate(gameObject);
								Debug.Log("not destroying object! it does not have a renderer");
							}
						}
					}
						

				}
			}
		}

	}
	//---
	void RemoveNavFromWhiteBoxAndCull()
	{
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		foreach (GameObject gameObject in allGameObjects)
		{
			if (gameObject.layer==LayerMask.NameToLayer("WhiteBox"))
			{
				//right, first see if the object has any important componenents on it
				bool leaveObject=false;
				bool hasRenderer=false;
				bool hasMesh=false;
				foreach(var component in gameObject.GetComponents<Component>())
				{
					//if its not a render mesh or a renderer then leave this object
					if(!( (component is Transform) ||  (component is MeshFilter) || component is MeshRenderer ))					
					{
						leaveObject=true;
					}					
					if(component is MeshFilter)
					{
						hasRenderer=true;
					}
					if(component is MeshRenderer)
					{
						hasMesh=true;
					}
					
				}
				if (leaveObject)
				{
					Log( LogMode.Objects, "skipping re-parent");
					
					continue;
				}
				else if (hasRenderer && hasMesh)
				{
					Transform parent=gameObject.transform.parent;
					//pull the children out and place them at this level
					//gameObject.transform.DetachChildren();
					List<Transform> children=new List<Transform>();
					foreach (Transform item in gameObject.transform)
					{
						children.Add(item);
					}
					foreach (Transform item in children)						
					{
						//item.gam
						Log( LogMode.Objects, "attempting re-parent");
						item.parent=parent;
					}
					GameObject.DestroyImmediate(gameObject);
					//gameObject.SetActive(false);
				}
			}
		}
	}
	bool CheckValidToCombine(MeshFilter candidateMesh,GameObject gameObject,int layerMask)
	{
		GameObject testObject=candidateMesh.gameObject;
		if (layerMask != 0 && testObject.layer!=layerMask)
		{
			return false;
		}
		if (testObject.tag == "AutoGeneratedVis" || testObject.tag == "AutoGeneratedPhys" || testObject.tag == "AutoGeneratedDepth" || testObject.tag == "AutoGeneratedRef" || testObject.tag == "AutoGeneratedMoveBlocker")
		{
			return false;
		}
		if (testObject.layer == LayerMask.NameToLayer("Switch") )
		{
			//switches are never combined
			return false;
		}
		MeshRenderer renderer = testObject.GetComponent<MeshRenderer>();
		if (renderer!= null && renderer.sharedMaterials.Length>1)
		{
			return false;
		}
		while (testObject.transform.parent!=gameObject.transform)
		{
			MeshCombineTag meshTag= testObject.GetComponent<MeshCombineTag>();
			if (meshTag!=null)
			{
				return false;
			}
			testObject=testObject.transform.parent.gameObject;
		}
		return !testObject.GetComponent<MeshCombineTag>();
	}
	
	static string CreateExportFolder( string currentScene, out string exportFolderPath )
	{
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Export";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		//delete the existing folder 
		Log( LogMode.Scenes, "deleting folder:"+exportFolderPath);
		//FileUtil.DeleteFileOrDirectory(folderToDelete);
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			System.IO.Directory.Delete(exportFolderPath,true);
			System.IO.Directory.CreateDirectory(exportFolderPath);
			//System.IO.Directory..GetFiles(
		}
		else
		{
			AssetDatabase.CreateFolder(parentFolderPath, exportFolderName);
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}
	static string CreatePhysicsFolder( string currentScene, out string exportFolderPath )
	{
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Physics";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		//delete the existing folder 
		Log( LogMode.Scenes, "deleting folder:"+exportFolderPath);
		//FileUtil.DeleteFileOrDirectory(folderToDelete);
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			System.IO.Directory.Delete(exportFolderPath,true);
			System.IO.Directory.CreateDirectory(exportFolderPath);
			//System.IO.Directory..GetFiles(
		}
		else
		{
			AssetDatabase.CreateFolder(parentFolderPath, exportFolderName);
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}

	static string CreateRipplesFolder( string currentScene, out string exportFolderPath )
	{
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Ripples";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		//delete the existing folder 
		Log( LogMode.Scenes, "deleting folder:"+exportFolderPath);
		//FileUtil.DeleteFileOrDirectory(folderToDelete);
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			System.IO.Directory.Delete(exportFolderPath,true);
			System.IO.Directory.CreateDirectory(exportFolderPath);
			//System.IO.Directory..GetFiles(
		}
		else
		{
			AssetDatabase.CreateFolder(parentFolderPath, exportFolderName);
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}
	static string CreateMoveBlockersFolder( string currentScene, out string exportFolderPath )
	{
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" MoveBlockers";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		//delete the existing folder 
		Log( LogMode.Scenes, "deleting folder:"+exportFolderPath);
		//FileUtil.DeleteFileOrDirectory(folderToDelete);
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			System.IO.Directory.Delete(exportFolderPath,true);
			System.IO.Directory.CreateDirectory(exportFolderPath);
			//System.IO.Directory..GetFiles(
		}
		else
		{
			AssetDatabase.CreateFolder(parentFolderPath, exportFolderName);
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}
	static string CreateReflectionsFolder( string currentScene, out string exportFolderPath )
	{
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Reflections";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		//delete the existing folder 
		Log( LogMode.Scenes, "deleting folder:"+exportFolderPath);
		//FileUtil.DeleteFileOrDirectory(folderToDelete);
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			System.IO.Directory.Delete(exportFolderPath,true);
			System.IO.Directory.CreateDirectory(exportFolderPath);
			//System.IO.Directory..GetFiles(
		}
		else
		{
			AssetDatabase.CreateFolder(parentFolderPath, exportFolderName);
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}
	static string CreateOrAddToCombinedFolder( string currentScene, out string exportFolderPath )
	{
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Combined";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		 
		//delete the existing folder 
		//Log( LogMode.Scenes, "deleting folder:"+exportFolderPath);
		//FileUtil.DeleteFileOrDirectory(folderToDelete);

		if (System.IO.Directory.Exists(exportFolderPath))
		{
			//System.IO.Directory.Delete(exportFolderPath,true);
			//System.IO.Directory.CreateDirectory(exportFolderPath);
			//System.IO.Directory..GetFiles(
		}
		else
		{
			AssetDatabase.CreateFolder(parentFolderPath, exportFolderName);
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}

	static string DeletePhysicsFolder( string currentScene )
	{
		string exportFolderPath;
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Physics";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			//System.IO.Directory.Delete(exportFolderPath,true);
			AssetDatabase.DeleteAsset(exportFolderPath);//.Refresh();
		}

		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}

	static string DeleteRipplesFolder( string currentScene)
	{
		string exportFolderPath;
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Ripples";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;

		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			//System.IO.Directory.Delete(exportFolderPath,true);
			AssetDatabase.DeleteAsset(exportFolderPath);//.Refresh();
		}

		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}

	static string DeleteReflectionsFolder( string currentScene )
	{
		string exportFolderPath;
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Reflections";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;
		
		if (System.IO.Directory.Exists(exportFolderPath))
		{
			//System.IO.Directory.Delete(exportFolderPath,true);
			AssetDatabase.DeleteAsset(exportFolderPath);//.Refresh();

		}

		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}
	static string DeleteCombinedFolder( string currentScene )
	{
		string exportFolderPath;
		string[] lines = currentScene.Split('/');
		Log( LogMode.Scenes, "scene name: "+lines[lines.Length-1]);
		string fileName=lines[lines.Length-1];
		
		string folderName=fileName.Split('.')[0];
		
		string exportFolderName = folderName+" Combined";
		
		string parentFolderPath = currentScene;
		parentFolderPath = parentFolderPath.Remove( parentFolderPath.LastIndexOf('/') );
		
		exportFolderPath = parentFolderPath + "/" + exportFolderName;

		if (System.IO.Directory.Exists(exportFolderPath))
		{
			//FileUtil.DeleteFileOrDirectory(exportFolderPath);
			//System.IO.Directory.Delete(exportFolderPath,true);
			AssetDatabase.DeleteAsset(exportFolderPath);//.Refresh();
		}
		
		return exportFolderPath+"/"+exportFolderName+"."+fileName.Split('.')[1];
	}

	/*
	 * Saves current scene, combines meshes, does light map offset and kicks off light map bake.
	*/
	void ExportCurrentScene()
	{
		//bool foundAnyCombineTags=false;
		//Log( LogMode.Scenes, "combining meshes");

		
		string origScene=EditorApplication.currentScene;
		
		Log( LogMode.Scenes, "saving scene before modifying: "+origScene);
		EditorApplication.SaveScene(origScene);
		
		// Disable debug logging etc in the exported scene:


		DeleteExportTags();

		DestroyAllObjectsWithTag("SourceMoveBlocker");
		DestroyAllObjectsWithTag("AutoGeneratedDepth");

		//create a folder path
		string exportFolderPath;
		_saveSceneName = CreateExportFolder( EditorApplication.currentScene, out exportFolderPath );
		
		MeshCombineTag[] allCombineTags=Object.FindObjectsOfType(typeof(MeshCombineTag)) as MeshCombineTag[];
		for (int i = 0; i < allCombineTags.Length; i++)
		{
			//show combined
			allCombineTags[i].ShowCombined();

			//delete source
		}


		//if (foundAnyCombineTags)
		//{
			//RemoveNavFromWhiteBoxAndCull();
		StripWhiteBoxAndCull();
		//}

		
		Log( LogMode.Scenes, "saving scene: "+_saveSceneName);
		EditorApplication.SaveScene(_saveSceneName);
		
		
		if (_fullLightBake)
		{
			DoBuildLightingOnly();
		}
	}
	void ExportCurrentSceneOld()
	{
		bool foundAnyCombineTags=false;
		Log( LogMode.Scenes, "combining meshes");
		
		
		string origScene=EditorApplication.currentScene;
		
		Log( LogMode.Scenes, "saving scene before modifying: "+origScene);
		EditorApplication.SaveScene(origScene);

		//create a folder path
		string exportFolderPath;
		_saveSceneName = CreateExportFolder( EditorApplication.currentScene, out exportFolderPath );
		
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		int hits = 0;
		foreach (GameObject gameObject in allGameObjects)
		{
			//for each mesh, go through all children (only direct children) and merge their meshes into an uber mesh
			//see if the object is tagged!
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			if (!meshTag)
			{
				// Not tagged, skip this object.
				continue;
			}
			
			foundAnyCombineTags=true;
			
			if( meshTag.meshTag!=MeshCombineTag.MeshTag.CombineAllBelow)
			{
				// Don't combine children.
				continue;
			}
			
			CombineObjectChildren( gameObject, exportFolderPath,"", ref hits );
		}
		
		if (foundAnyCombineTags)
		{
			RemoveNavFromWhiteBoxAndCull();
		}
		
		//find checkpoint 0 and move player to it!
		PlacePlayerAtCheckPoint0();
		
		//if( false )
#if false
		{
			// Abort!
			_generatingLightMapsInProgress = false;
			return;
		}
#endif
		
		Log( LogMode.Scenes, "saving scene: "+_saveSceneName);
		EditorApplication.SaveScene(_saveSceneName);
		
		
		if (_fullLightBake)
		{
			DoBuildLightingOnly();
		}
	}
	//--
	//public List<GameObject> tempSplitObjects = new List<GameObject>();
	void DestroyTempSplitMeshes()
	{
		DestroyAllObjectsWithTag("AutoGeneratedMultiMesh");
	}
	void TestAndSplitMultiMaterialMeshes(Transform Parent)
	{
		MeshRenderer[] meshChildren = Parent.GetComponentsInChildren<MeshRenderer>() as MeshRenderer[];
		for (int i = 0; i < meshChildren.Length; i++)
		{
			if (meshChildren[i].sharedMaterials.Length>1)
			{
				//found multi mesh!
				Debug.Log("found multi mesh");
				MeshFilter meshfilter = meshChildren[i].gameObject.GetComponent<MeshFilter>();
				if (meshfilter)
				{

					if (meshfilter.sharedMesh.subMeshCount>1)
					{
						//optimal version
						for (int subMesh = 0; subMesh < meshfilter.sharedMesh.subMeshCount; subMesh++)
						{
							int[] origTris = meshfilter.sharedMesh.GetTriangles(subMesh);

							List<int> usedVerts = new List<int>();
							int[] triangleRemap = new int[origTris.Length];
							int nextIndex = 0;
							for (int tri = 0; tri < origTris.Length;tri++)
							{
								int vertID = origTris[tri];
								if (usedVerts.Contains(vertID))
								{
									triangleRemap[tri] = usedVerts.IndexOf(vertID);
									continue;
								}

								usedVerts.Add(vertID);
								triangleRemap[tri] = nextIndex;
								nextIndex++;
							}

							//copy these tris and verts normals etc into another mesh 
							//int[] tris = triangleRemap;
							Mesh newMesh = new Mesh();
							//for now, do this sub optimal, come back and improve later!
							int[] newTris = new int[triangleRemap.Length];
							Vector3[] newVerts = new Vector3[usedVerts.Count];
							Vector3[] newNormals = new Vector3[usedVerts.Count];
							Vector2[] newUV0s = new Vector2[usedVerts.Count];
							Vector2[] newUV1s = new Vector2[usedVerts.Count];
							Vector2[] newUV2s = new Vector2[usedVerts.Count];

							for (int vertItt = 0; vertItt < triangleRemap.Length; vertItt++)
							{
								newTris[vertItt] = triangleRemap[vertItt];
								int subIndex = triangleRemap[vertItt];
								int index = usedVerts[subIndex];
								newVerts[subIndex] = meshfilter.sharedMesh.vertices[index];
								newNormals[subIndex] = meshfilter.sharedMesh.normals[index];
								if (meshfilter.sharedMesh.uv.Length>0)
								{
									newUV0s[subIndex] = meshfilter.sharedMesh.uv[index];
								}
								if (meshfilter.sharedMesh.uv1.Length>0)
								{
									newUV1s[subIndex] = meshfilter.sharedMesh.uv1[index];
								}
								if (meshfilter.sharedMesh.uv2.Length>0)
								{
									newUV2s[subIndex] = meshfilter.sharedMesh.uv2[index];
								}
							}
							//add a mesh renderer
							newMesh.vertices = newVerts;
							newMesh.triangles = newTris;
							newMesh.normals = newNormals;
							if (meshfilter.sharedMesh.uv.Length>0)
							{
								newMesh.uv = newUV0s;
							}
							if (meshfilter.sharedMesh.uv1.Length>0)
							{
								newMesh.uv1 = newUV1s;
							}
							if (meshfilter.sharedMesh.uv2.Length>0)
							{
								newMesh.uv2 = newUV2s;
							}
							
							GameObject tempSplitObject=new GameObject("_TempSplit");
							tempSplitObject.transform.parent = meshChildren[i].transform.parent;
							tempSplitObject.transform.position = meshfilter.transform.position;
							tempSplitObject.transform.rotation = meshfilter.transform.rotation;
							tempSplitObject.transform.localScale = meshfilter.transform.localScale;
							MeshFilter newMeshFilter = tempSplitObject.AddComponent<MeshFilter>();
							newMeshFilter.sharedMesh = newMesh;
							MeshRenderer newMeshRenderer = tempSplitObject.AddComponent<MeshRenderer>();
							newMeshRenderer.sharedMaterial = meshChildren[i].sharedMaterials[subMesh];
							//tempSplitObjects.Add(tempSplitObject);
							tempSplitObject.tag = "AutoGeneratedMultiMesh";
						}
						//old version
						/*
						for (int subMesh = 0; subMesh < meshfilter.sharedMesh.subMeshCount; subMesh++)
						{
							//copy these tris and verts normals etc into another mesh 
							int[] tris = meshfilter.sharedMesh.GetTriangles(subMesh);
							Mesh newMesh = new Mesh();
							//for now, do this sub optimal, come back and improve later!
							int[] newTris = new int[tris.Length];
							Vector3[] newVerts = new Vector3[tris.Length];
							Vector3[] newNormals = new Vector3[tris.Length];
							Vector2[] newUV0s = new Vector2[tris.Length];
							Vector2[] newUV1s = new Vector2[tris.Length];
							Vector2[] newUV2s = new Vector2[tris.Length];
							for (int vertItt = 0; vertItt < tris.Length; vertItt++)
							{
								newTris[vertItt] = vertItt;
								newVerts[vertItt] = meshfilter.sharedMesh.vertices[tris[vertItt]];
								newNormals[vertItt] = meshfilter.sharedMesh.normals[tris[vertItt]];
								if (meshfilter.sharedMesh.uv.Length>0)
								{
									newUV0s[vertItt] = meshfilter.sharedMesh.uv[tris[vertItt]];
								}
								if (meshfilter.sharedMesh.uv1.Length>0)
								{
									newUV1s[vertItt] = meshfilter.sharedMesh.uv1[tris[vertItt]];
								}
								if (meshfilter.sharedMesh.uv2.Length>0)
								{
									newUV2s[vertItt] = meshfilter.sharedMesh.uv2[tris[vertItt]];
								}
							}
							//add a mesh renderer
							newMesh.vertices = newVerts;
							newMesh.triangles = newTris;
							newMesh.normals = newNormals;
							if (meshfilter.sharedMesh.uv.Length>0)
							{
								newMesh.uv = newUV0s;
							}
							if (meshfilter.sharedMesh.uv1.Length>0)
							{
								newMesh.uv1 = newUV1s;
							}
							if (meshfilter.sharedMesh.uv2.Length>0)
							{
								newMesh.uv2 = newUV2s;
							}

							GameObject tempSplitObject=new GameObject("_TempSplit");
							tempSplitObject.transform.parent = meshChildren[i].transform.parent;
							tempSplitObject.transform.position = meshfilter.transform.position;
							tempSplitObject.transform.rotation = meshfilter.transform.rotation;
							tempSplitObject.transform.localScale = meshfilter.transform.localScale;
							MeshFilter newMeshFilter = tempSplitObject.AddComponent<MeshFilter>();
							newMeshFilter.sharedMesh = newMesh;
							MeshRenderer newMeshRenderer = tempSplitObject.AddComponent<MeshRenderer>();
							newMeshRenderer.sharedMaterial = meshChildren[i].sharedMaterials[subMesh];
							//tempSplitObjects.Add(tempSplitObject);
							tempSplitObject.tag = "AutoGeneratedMultiMesh";
						}
						*/
					}
				}
			}
		}
		//AutoGeneratedMultiMesh
	}
	//--
	public List<MeshFilter> combinedMeshesInLastBatch = new List<MeshFilter>();
	public List<MeshFilter> generatedMeshesInLastBatch = new List<MeshFilter>();
	public void CombineChildren(Transform node)
	{
		combinedMeshesInLastBatch.Clear();
		generatedMeshesInLastBatch.Clear();
		//bool foundAnyCombineTags=false;
		Log( LogMode.Scenes, "combining meshes");
		string exportFolderPath;
		CreateOrAddToCombinedFolder( EditorApplication.currentScene, out exportFolderPath );

		//run through all children and check for any stuff taged as built
		//delete any we find
		MeshFilter[] meshChildren = node.GetComponentsInChildren<MeshFilter>() as MeshFilter[];
		for (int num = 0; num < meshChildren.Length; num++)
		{
			if (meshChildren[num].tag == "AutoGeneratedVis")
			{
				GameObject.DestroyImmediate(meshChildren[num].gameObject);
			}
		}

		TestAndSplitMultiMaterialMeshes(node);
		//string origScene=EditorApplication.currentScene;
		
		//Log( LogMode.Scenes, "saving scene before modifying: "+origScene);
		//EditorApplication.SaveScene(origScene);
		
		// Disable debug logging etc in the exported scene:
		//DisableSceneDebugOptions();
		
		//create a folder path
		//string exportFolderPath;
		//_saveSceneName = CreateExportFolder( EditorApplication.currentScene, out exportFolderPath );
		
		Transform[] allGameObjects=node.GetComponentsInChildren<Transform>();// Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		int hits = 0;
		foreach (Transform gameObject in allGameObjects)
		{
			//for each mesh, go through all children (only direct children) and merge their meshes into an uber mesh
			//see if the object is tagged!
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			if (!meshTag)
			{
				// Not tagged, skip this object.
				continue;
			}
			
			//foundAnyCombineTags=true;
			
			if( meshTag.meshTag!=MeshCombineTag.MeshTag.CombineAllBelow)
			{
				// Don't combine children.
				continue;
			}
			
			CombineObjectChildren( gameObject.gameObject, exportFolderPath,"", ref hits );
		}

		DestroyTempSplitMeshes();
		//AssetDatabase.GenerateUniqueAssetPath
		AssetDatabase.Refresh(); 
		AssetDatabase.SaveAssets();


		return;
		
	}//--

	
	void CombineObjectChildren (GameObject gameObject, string newAssetsPath, string namePrefix, ref int hits ,int layerMask = 0,bool AllowBlockers = false)
	{
		MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
		
		MeshFilter[] childMeshCandidates=gameObject.GetComponentsInChildren<MeshFilter>();
		List<MeshFilter> childMeshes = new List<MeshFilter>();
		
		//find out how many types of material we are going to need to split into
		List<Material> uniqueMatList=new List<Material>(); 
		uniqueMatList.Clear();	
		foreach(MeshFilter candidateMesh in childMeshCandidates)
		{
			//todo, this part should be altered to support increased controll of merging in editor

			bool valid=CheckValidToCombine(candidateMesh,gameObject,layerMask);
			if (!AllowBlockers && candidateMesh.gameObject.layer == LayerMask.NameToLayer("MoveBlocker"))
			{
				valid = false;
			}
			if (valid && candidateMesh.sharedMesh!=null)				
				//if (candidateMesh.gameObject.transform.parent==gameObject.transform && candidateMesh.sharedMesh!=null)
			{
				MeshRenderer thisRenderer=candidateMesh.gameObject.GetComponent<MeshRenderer>();
				//DBG.Assert (thisRenderer!=null);
				//DebugUtils.DebugAssert (candidateMesh.sharedMesh.subMeshCount==1);
				if (uniqueMatList.Count==0)
				{
					uniqueMatList.Add(thisRenderer.sharedMaterial);
				}
				else if (!uniqueMatList.Contains(thisRenderer.sharedMaterial) && thisRenderer.sharedMaterial!=null)
				{
					//insert into list
					uniqueMatList.Add(thisRenderer.sharedMaterial);
				}
				childMeshes.Add(candidateMesh);
			}
		}
		List<MeshFilter> meshesToCombine = new List<MeshFilter>();
		List<Mesh> combinedMeshes = new List<Mesh>();
		//put meshes into different lists depending on material
		
		Log( LogMode.Objects, "Combining meshes for: "+gameObject.name, gameObject); 			
		Log( LogMode.Objects, "Num materials: "+uniqueMatList.Count);


		for (int num=0;num<uniqueMatList.Count;num++)
		{
			Log( LogMode.Objects, "material name: "+uniqueMatList[num].name);
			
			meshesToCombine.Clear();
			foreach(MeshFilter candidateMesh in childMeshes)
			{
				MeshRenderer thisRenderer=candidateMesh.gameObject.GetComponent<MeshRenderer>();
				Log( LogMode.Objects, "material test name: "+thisRenderer.sharedMaterial.name + " for object " + candidateMesh.name, candidateMesh.gameObject);
	
				if (uniqueMatList[num]==thisRenderer.sharedMaterial && ((_excludeWhiteBox==false) || candidateMesh.gameObject.layer!=LayerMask.NameToLayer("WhiteBox")))
				{
					meshesToCombine.Add(candidateMesh);
					combinedMeshesInLastBatch.Add(candidateMesh);
				}
			}
			if (meshesToCombine.Count>=1)
			{		
				Log( LogMode.Objects, "combining " + meshesToCombine.Count + " children of:"+gameObject.name );
				
				Mesh combinedMesh=CreateCombinedMesh(meshesToCombine,gameObject);
				combinedMesh.name=("_Combined"+namePrefix+gameObject.name+"Child");
				combinedMeshes.Add(combinedMesh);
				Log( LogMode.Objects, "verts=:"+combinedMesh.vertexCount);

				AssetDatabase.CreateAsset(combinedMesh, AssetDatabase.GenerateUniqueAssetPath(newAssetsPath+"/"+"_Combined"+namePrefix+gameObject.name+hits+"_"+num+".asset"));
				AssetDatabase.SaveAssets();
				hits++;
			}
			
		}
		//now we have the combined meshes, instanciate them and remove the old meshes
		//note that this may be self reflexive :( todo, fix
		for (int num=0;num<combinedMeshes.Count;num++)
		{
			//SceneView.CreateInstance
			GameObject testObject=new GameObject("_Combined"+namePrefix+gameObject.name+"Child"+"_"+uniqueMatList[num].name);
			testObject.transform.rotation=gameObject.transform.rotation;
			testObject.transform.position=gameObject.transform.position;
			testObject.transform.parent=gameObject.transform;
			testObject.transform.localScale=new Vector3(1.0f,1.0f,1.0f);//gameObject.transform.localScale;
			testObject.transform.localPosition=new Vector3(0.0f,0.0f,0.0f);
			testObject.transform.localRotation=Quaternion.identity;
			
			testObject.AddComponent<MeshFilter>();
			testObject.AddComponent<MeshRenderer>();
			testObject.GetComponent<MeshFilter>().sharedMesh=combinedMeshes[num];
			//testObject.GetComponent<MeshFilter>().can
			testObject.GetComponent<MeshRenderer>().sharedMaterial=uniqueMatList[num];
			testObject.layer=LayerMask.NameToLayer("Default");
			testObject.tag = "AutoGeneratedVis";
			generatedMeshesInLastBatch.Add(testObject.GetComponent<MeshFilter>());
			//flag the object as static lightmapping always
			
			StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(testObject);
			
			if(meshTag.staticLightMap)
			{
				staticFlags |= StaticEditorFlags.LightmapStatic;
			}
			
			if(meshTag.staticBatch)
			{
				staticFlags |= StaticEditorFlags.BatchingStatic;
			}
			
			GameObjectUtility.SetStaticEditorFlags(testObject, staticFlags);
			
			//lightMapUtil
			//if flagged, add the new object to the lightmap offset utility
			//lightMapUtil.gameObjectsArray.
			
		}
		//remove the old meshes
		foreach(MeshFilter candidateMesh in childMeshes)
		{
			// pash: I think this is to ensure that the object gets culled in the later 'remove whitebox' step.
			if (candidateMesh.gameObject.layer!=LayerMask.NameToLayer("MoveBlocker"))
			{
				Debug.LogError( "TODO mark objects for deletion without changing their layer" );
				//candidateMesh.gameObject.layer=LayerMask.NameToLayer("WhiteBox");
			}
			//MeshRenderer renderer=candidateMesh.gameObject.GetComponent<MeshRenderer>();
			//DestroyImmediate(candidateMesh);				
			//DestroyImmediate(renderer);
		}
		//AssetDatabase.Refresh(); 
		//AssetDatabase.SaveAssets();
	}	
	
	void PlacePlayerAtCheckPoint0()
	{

	}
	//int getReverseID(int vertID,int triID,bool reverse)
	int getReverseID(int ID,int size,bool reverse)	
	{
		if (reverse)
		{
			//Debug.Log("reverse winding inner!");
			//int triStart=vertID-(id%3);
			//int triEnd=triStart+3;
			//int reverseId=triStart+(triEnd-(vertID+1));
			
			//return reverseId;//size-(id+1);
			return size-(ID+1);
		}
		else
		{
			return ID;
		}
	}
	
	
	public static int SortByMeshArea(MeshFilter obj1, MeshFilter obj2) 
	{
		float diff = GetMeshUVScale( obj2 ) - GetMeshUVScale( obj1 );
		return diff < 0 ? -1 : diff > 0 ? 1 : 0;
	}
	
	static int[] edgeLengths = { 1, 2, 4, 8 };
	
	// This returns an int which corresponds to the side length that should be allocated for this mesh.
	static int CategoriseScale (float actualScale)
	{
		for( int i=0 ; i<edgeLengths.Length-1 ; i++ )
		{
			float cutoff = edgeLengths[i]*edgeLengths[i];//0.5f*(float)(edgeLengths[i]*edgeLengths[i] + edgeLengths[i+1]*edgeLengths[i+1]);
			if( actualScale < cutoff )
			{
				//Debug.Log( "Cutoff " + i + "=" + cutoff );
				return i;
			}
		}
		return edgeLengths.Length-1;
	}
	
	int NextSquare (int num, out int root )
	{
		root=0;
		while( root*root < num )
		{
			root++;
		}
		return root*root;
	}
	
	int NextPowerOfFour (int num, out int powerOfTwo )
	{
		int powerOfFour = 1;
		powerOfTwo = 1;
		while( powerOfFour < num )
		{
			powerOfFour *= 4;
			powerOfTwo *= 2;
		}
		return powerOfFour;
	}
	
	void GetCoords( int value, out int x, out int y )
	{
		int initialVal = value;
		int remainder = value;
		int powerOfFour = 1;
		int powerOfTwo = 1;
		x=0;
		y=0;
		while( remainder > 0 )
		{
			int nextPowerOfFour = 4 * powerOfFour;
			int multiples = (initialVal % nextPowerOfFour) / powerOfFour;
			x += (powerOfTwo) * (multiples % 2);
			y += (powerOfTwo) * (multiples / 2);
			
			remainder -= multiples * powerOfFour;
			
			powerOfFour = nextPowerOfFour;
			powerOfTwo *= 2;
		}
		
		//Debug.Log( "Get Coords " + value + " = " + x + "," + y );
	}
	
	void TestCoords()
	{
		return;
#if false
		int x=0;
		int y=0;
		GetCoords( 5, out x, out y );
		DebugUtils.DebugAssert( x==3 && y==0 );
		
		GetCoords( 11, out x, out y );
		DebugUtils.DebugAssert( x==1 && y==3 );
		
		GetCoords( 31, out x, out y );
		DebugUtils.DebugAssert( x==7 && y==3 );
		
		GetCoords( 16, out x, out y );
		DebugUtils.DebugAssert( x==4 && y==0 );
#endif
	}
	
	Mesh CreateCombinedMesh(List<MeshFilter> childMeshes,GameObject parentObject)
	{
		Mesh combinedMesh = new Mesh();
		
		List<int> indexList = new List<int>();
		List<Color> colourList = new List<Color>();
		List<Vector3> vertexList = new List<Vector3>();
		List<Vector3> normalList = new List<Vector3>();
		List<Vector2> uvList = new List<Vector2>();
		List<Vector2> uv2List = new List<Vector2>();		
		List<Vector2> uv3List = new List<Vector2>();
		
		int indexStart = 0;
		
		Log( LogMode.Meshes, "mesh combine count="+childMeshes.Count);
		
		float uvPadding = 2.0f/1024.0f;
		
		// Sort list by area:
		childMeshes.Sort( SortByMeshArea );
		
		// Decide UV scale:
		int maxEdgeLengthCat = 0;
		int[] edgeCatCount = { 0,0,0,0 };
		
		for (int num=0;num<childMeshes.Count;num++)
		{
			int edgeLengthCat = CategoriseScale( GetMeshUVScale( childMeshes[num] ) );
			maxEdgeLengthCat = Mathf.Max( edgeLengthCat, maxEdgeLengthCat );
			edgeCatCount[edgeLengthCat]++;
		}
		
		for( int i=0 ; i<edgeLengths.Length ; i++ )
		{
			Log( LogMode.Meshes, "**** catCount " + edgeLengths[i] + "=" + edgeCatCount[i] );
		}
		
		int maxEdgeLen = edgeLengths[maxEdgeLengthCat];
		Log( LogMode.Meshes, "**** Max edge len = " + maxEdgeLen );
		int totalRequiredLargeSquares = 0;
		for( int i=0 ; i<=maxEdgeLengthCat ; i++ )
		{
			int numInOneLargeSquare = (maxEdgeLen*maxEdgeLen)/(edgeLengths[i]*edgeLengths[i]);
			int requiredLargeSquares = Mathf.CeilToInt( ((float)edgeCatCount[i] / (float)numInOneLargeSquare) );
			Log( LogMode.Meshes, "Num " + maxEdgeLen + "x" + maxEdgeLen + "squares reqd for " + edgeCatCount[i] + " " + edgeLengths[i] + "x" + edgeLengths[i] + " =" + requiredLargeSquares );
			totalRequiredLargeSquares += requiredLargeSquares;
		}
		int numPerSide = 0;
		int assignedLargeSquares = NextPowerOfFour( totalRequiredLargeSquares, out numPerSide );
		float bigSquareUVSideLen = 1.0f / (float)numPerSide;
		float unitSquareUVSideLen = bigSquareUVSideLen / (float)maxEdgeLen;
		Log( LogMode.Meshes, "**** Max edge len = " + maxEdgeLen + ", num reqd=" + totalRequiredLargeSquares + " sq=" + assignedLargeSquares + " bigSqSide=" + bigSquareUVSideLen);
		
		int currEdgeCat = maxEdgeLengthCat;
		int unitSquareCount = 0;
		int meshIdx = 0;
		while( currEdgeCat >= 0 )
		{
			for (;meshIdx<childMeshes.Count;meshIdx++)
			{
				MeshFilter childMeshFilter = childMeshes[meshIdx];
				int edgeCat = CategoriseScale( GetMeshUVScale( childMeshFilter ) );
				if( edgeCat != currEdgeCat )
				{
					break;
				}
				
				int x=0;
				int y=0;
				GetCoords( unitSquareCount, out x, out y );
				
				Vector2 uvPos = unitSquareUVSideLen * new Vector2( x, y ) + new Vector2(uvPadding, uvPadding);
				float uvScale = edgeLengths[currEdgeCat] * unitSquareUVSideLen - 2.0f*uvPadding;
				
				
				
				//if there is an odd number of negative scale then reverse the order 		
				//Debug.Log("child mesh num ="+num);
				Matrix4x4 localMat=parentObject.transform.worldToLocalMatrix*childMeshFilter.transform.localToWorldMatrix;
				//Vector3.Dot(Vector3.Cross(localMat.GetRow(0),ocalMat.GetRow(1)),ocalMat.GetRow(2))<0.0f
				int numNegScales=0;
				if (childMeshFilter.transform.localScale.x<0.0f)
				{
					numNegScales++;
				}
				if (childMeshFilter.transform.localScale.y<0.0f)
				{
					numNegScales++;
				}
				if (childMeshFilter.transform.localScale.z<0.0f)
				{
					numNegScales++;
				}
				bool reversAllWindings=false;
				//if ((numNegScales%2)!=0)
				if (Vector3.Dot(Vector3.Cross(localMat.GetRow(0),localMat.GetRow(1)),localMat.GetRow(2))<0.0f)
				{
					//Debug.Log("reverse winding!");
					reversAllWindings=true;
				} 
				else
				{
					//Debug.Log("normal winding!");			
				}
				
				if (childMeshFilter.sharedMesh==null)
				{
					Log(LogMode.Warning, "found null mesh!");
					continue;
				}
				
				
				//Matrix4x4 localMat=new Matrix4x4();
				//localMat.SetTRS(childMeshFilter.transform.localPosition,childMeshFilter.transform.localRotation,childMeshFilter.transform.localScale);
				Matrix4x4 noPosMat=localMat;
				noPosMat.SetColumn(3,new Vector4(0.0f,0.0f,0.0f,1.0f));
				//Matrix4x4 noPosMat=new Matrix4x4();
				//noPosMat.SetTRS(new Vector4(0.0f,0.0f,0.0f,1.0f),childMeshFilter.transform.localRotation,childMeshFilter.transform.localScale);
				
				//childMeshFilter.mesh.colors
				Color[] colours=childMeshFilter.sharedMesh.colors;
				if (colours.Length<childMeshFilter.sharedMesh.vertices.Length)
				{
					colours=new Color[childMeshFilter.sharedMesh.vertices.Length];
				}
				for (int numColours=0;numColours<colours.Length;numColours++)
				{
					colourList.Add(colours[numColours]);
				}	
				
				
				Vector3[] verts=childMeshFilter.sharedMesh.vertices;
				for (int numVerts=0;numVerts<verts.Length;numVerts++)
				{
					vertexList.Add(localMat.MultiplyPoint3x4(verts[numVerts]));
				}
				
				int[] tris=childMeshFilter.sharedMesh.triangles;
				for (int numIndices=0;numIndices<tris.Length;numIndices++)
				{
					indexList.Add(tris[getReverseID(numIndices,tris.Length,reversAllWindings)]+indexStart);
				}
				indexStart+=verts.Length;
				
				
				
				Vector3[] normals=childMeshFilter.sharedMesh.normals;
				Matrix4x4 noScaleMat = noPosMat;
				noScaleMat.SetRow(3,new Vector4(0.0f,0.0f,0.0f,1.0f));
				noScaleMat.SetColumn(0,noScaleMat.GetColumn(0).normalized);
				noScaleMat.SetColumn(1,noScaleMat.GetColumn(1).normalized);
				noScaleMat.SetColumn(2,noScaleMat.GetColumn(2).normalized);

				Vector3 recipricalLocalScale = new Vector3(1.0f/childMeshFilter.transform.localScale.x,1.0f/childMeshFilter.transform.localScale.y,1.0f/childMeshFilter.transform.localScale.z);

				if (childMeshFilter.transform.localScale.x<0.0f)
				{
					noScaleMat.SetColumn(0,-noScaleMat.GetColumn(0));
				}
				if (childMeshFilter.transform.localScale.y<0.0f)
				{
					noScaleMat.SetColumn(1,-noScaleMat.GetColumn(1));
				}
				if (childMeshFilter.transform.localScale.z<0.0f)
				{
					noScaleMat.SetColumn(2,-noScaleMat.GetColumn(2));
				}


				for (int numNormals=0;numNormals<normals.Length;numNormals++)
				{

					Vector3 scaledNormal = normals[numNormals];
					scaledNormal.Scale(recipricalLocalScale);
					//scaledNormal.Normalize();
					Vector3 mulNormal=noScaleMat.MultiplyVector(scaledNormal).normalized;

					normalList.Add(mulNormal);
				}
				
				Vector2[] uvs1=childMeshFilter.sharedMesh.uv;
				Vector2[] uvs2=childMeshFilter.sharedMesh.uv1;
				Vector2[] uvs3=childMeshFilter.sharedMesh.uv2;
				if (uvs2.Length<uvs1.Length)
				{
					Log(LogMode.Warning,"missing uv2!!!");
					uvs2=uvs1;
				}
				if (uvs3.Length<uvs1.Length)
				{
					Log(LogMode.Warning,"Swapping uv2<>uv3");
					uvs3=uvs2;
				}
				for (int numUV1=0;numUV1<uvs1.Length;numUV1++)
				{
					// Copy UV1 unchanged.
					uvList.Add(uvs1[numUV1]);
				}
				for (int numUV2=0;numUV2<uvs2.Length;numUV2++)
				{
					//uvs2[numUV2].x*=childMeshFilter.renderer.lightmapTilingOffset.x;
					//uvs2[numUV2].y*=childMeshFilter.renderer.lightmapTilingOffset.x;
					Vector2 newUV=(uvs2[numUV2]*uvScale)+uvPos;
					if (newUV.x>1.0f || newUV.x<0.0f)
					{
						Log(LogMode.Warning, "bad uv2 x! "+childMeshFilter.name + "Value="+uvs2[numUV2].x+"new value="+newUV.x+"offset u="+uvPos);
					}
					if (newUV.y>1.0f || newUV.y<0.0f)
					{
						Log(LogMode.Warning, "bad uv2 y! "+childMeshFilter.name + "Value="+uvs2[numUV2].y+"new value="+newUV.y+"offset u="+uvPos);
					}
					uv2List.Add(newUV);
				}
				for (int numUV3=0;numUV3<uvs3.Length;numUV3++)
				{
					Vector2 newUV=(uvs3[numUV3]*uvScale)+uvPos;				
					uv3List.Add(newUV);//uvs3[numUV3]);
				}	
				
				unitSquareCount += edgeLengths[currEdgeCat]*edgeLengths[currEdgeCat];
			}
			
			currEdgeCat--;
		}
		//now go through all the verts and snap close verts togeather
		for (int num=0;num<vertexList.Count;num++)
		{			
			for (int numInner=num+1;numInner<vertexList.Count;numInner++)
			{
				float differenceSq=(vertexList[num]-vertexList[numInner]).sqrMagnitude;
				if (differenceSq<_snapDistance*_snapDistance)
				{
					//Log(LogMode.Meshes, "snapping vert, differenceSq= "+differenceSq);
					vertexList[numInner]=vertexList[num];
				}
			}
		}
		for (int num=0;num<vertexList.Count;num++)
		{			
			for (int numInner=num+1;numInner<vertexList.Count;numInner++)
			{
				float differenceSq=(vertexList[num]-vertexList[numInner]).sqrMagnitude;
				if (differenceSq<_snapDistance*_snapDistance && differenceSq>0.0f)
				{
					Log(LogMode.Meshes, "re snapping!!");
					vertexList[numInner]=vertexList[num];
				}
			}
		}
		combinedMesh.vertices=vertexList.ToArray();
		combinedMesh.triangles=indexList.ToArray();
		combinedMesh.colors=colourList.ToArray();
		
		Log(LogMode.Meshes, "mesh verts="+vertexList.Count);
		Log(LogMode.Meshes, "mesh normals="+normalList.Count);
		
		combinedMesh.normals=normalList.ToArray();		
		//Debug.Log("mesh uv="+uvList.Count);
		//Debug.Log("mesh uv2="+uv2List.Count);
		//Debug.Log("mesh uv3="+uv3List.Count);
		combinedMesh.uv=uvList.ToArray();		
		//combinedMesh.uv1=uv2List.ToArray();		
		//combinedMesh.uv2=uv3List.ToArray();		
		
		UnwrapParam unwrapParam = new UnwrapParam();
		UnwrapParam.SetDefaults( out unwrapParam );
		//Debug.LogError( "" + unwrapParam.angleError + " " + unwrapParam.areaError + " " + unwrapParam.hardAngle + " " + unwrapParam.packMargin );
		unwrapParam.packMargin *= 2.0f;
		//Debug.LogError( "" + unwrapParam.angleError + " " + unwrapParam.areaError + " " + unwrapParam.hardAngle + " " + unwrapParam.packMargin );
		try
		{
			Unwrapping.GenerateSecondaryUVSet( combinedMesh, unwrapParam );
		}
		catch( UnityException e )
		{
			Debug.LogError( "Couldn't build UVs for " + combinedMesh.name + " error: " + e.Message, combinedMesh );
		}
		return combinedMesh;
	}
	
	static float GetMeshUVScale (MeshFilter meshFilter )
	{
		SerializedObject so = new SerializedObject (meshFilter.gameObject.renderer);
		float scale = so.FindProperty("m_ScaleInLightmap").floatValue;
		
		if( scale > 1 )
		{
			Log( LogMode.Meshes, "Mesh " + meshFilter.gameObject.name + " scale=" + scale );
		}
		
		if( meshFilter.sharedMesh == null )
		{
			Debug.LogError( "Missing mesh for " + meshFilter.name, meshFilter );
		}
		if( _meshScaleMap.ContainsKey( meshFilter.sharedMesh ) )
		{
			return scale * _meshScaleMap[meshFilter.sharedMesh];
		}
		
		// Calculate mesh scale:
		float totalArea = 0.0f;
		int[] triangles = meshFilter.sharedMesh.GetTriangles(0);
		Vector3[] verts = meshFilter.sharedMesh.vertices;
		for( int i=0 ; i<triangles.Length ; i+=3 )
		{
			Vector3 v12 = verts[triangles[i+1]] - verts[triangles[i]];
			Vector3 v13 = verts[triangles[i+2]] - verts[triangles[i]];
			float area = 0.5f*Vector3.Cross(v12, v13).magnitude;
			totalArea += area;
		}
		
		_meshScaleMap[meshFilter.sharedMesh] = totalArea;
		
		Log( LogMode.Meshes, "Mesh " + meshFilter.sharedMesh.name + " has area " + totalArea + " cat=" + CategoriseScale(totalArea) );
		
		return scale * totalArea;
	}
	
	void OffsetMeshesAndStartBakeLightMap()
	{
		Log(LogMode.Scenes, "build light map");
		
		DoLightMapOffsetPosition();
		
		_generatingLightMapsInProgress = true;
		
		Lightmapping.BakeAsync();
	}
	
	void DoLightMapOffsetPosition()
	{
		LightMapOffset lightMapUtil=_newlightmapExportGO.GetComponent<LightMapOffset>();
		
		lightMapUtil.MoveGameObjects();
	}
	
	public void UndoLightMapOffsetPosition()
	{
		if( null!=_newlightmapExportGO )
		{
			LightMapOffset lightMapUtil=_newlightmapExportGO.GetComponent<LightMapOffset>();
			for (int num=0;num<10;num++)
			{
				lightMapUtil.RestoreGameObjectPositions();
			}
			GameObject.DestroyImmediate(_newlightmapExportGO);
		}
	}
	
	public void DeleteExistingBuiltPhysics()
	{
		MeshCollider[] autoPhysicsObjects = GameObject.FindObjectsOfType<MeshCollider>();//.FindGameObjectsWithTag("AutoPhysics");
		for (int num=0; num<autoPhysicsObjects.Length;num++)
		{ 
			if (autoPhysicsObjects[num].gameObject.name.Contains("_CombinedPhysics"))
			{
				GameObject.DestroyImmediate(autoPhysicsObjects[num].gameObject);
			}
		}
		//delete old data
		DestroyAllObjectsWithTag("AutoGeneratedPhys");

		
	}
	public void BuildPhysicsData()
	{
		string exportFolderPath;
		_saveSceneName = CreatePhysicsFolder( EditorApplication.currentScene, out exportFolderPath );
		
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		foreach (GameObject gameObject in allGameObjects)
		{
			//for each mesh, go through all children (only direct children) and merge their meshes into an uber mesh
			//see if the object is tagged!
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			if (!meshTag)
			{
				// Not tagged, skip this object.
				continue;
			}
			TestAndSplitMultiMaterialMeshes(gameObject.transform);
			if (!meshTag.PhysicsData)
			{
				continue;
			}
			//foundAnyCombineTags=true;
			
			if( meshTag.meshTag!=MeshCombineTag.MeshTag.CombineAllBelow)
			{
				// Don't combine children.
				continue;
			}

			BuildPhysicsChildData( gameObject, exportFolderPath);

			DestroyTempSplitMeshes();

		}
		
		//EditorApplication.SaveScene (_saveSceneName);
	}

	void BuildPhysicsChildData(GameObject gameObject, string exportFolderPath)
	{
		MeshFilter[] meshesToCombine=gameObject.GetComponentsInChildren<MeshFilter>();
		List<MeshFilter> meshesToCombineList = new List<MeshFilter>();
		for (int num=0;num<meshesToCombine.Length;num++)
		{
			if (!CheckValidToCombine(meshesToCombine[num],gameObject,0))
			{
				continue;
			}
			meshesToCombineList.Add(meshesToCombine[num]);
		}
		//TODO: check there is not a dont combine tag in heirarchy
		
		Mesh combinedMesh=CreateCombinedMesh(meshesToCombineList,gameObject);
		
		combinedMesh.name=("_CombinedPhysics"+gameObject.name);
		//combinedMeshes.Add(combinedMesh);
		
		//--- instanciate the object
		GameObject testObject=new GameObject("_CombinedPhysics"+gameObject.name);
		testObject.transform.rotation=gameObject.transform.rotation;
		testObject.transform.position=gameObject.transform.position;
		testObject.transform.parent=gameObject.transform;
		testObject.transform.localScale=new Vector3(1.0f,1.0f,1.0f);//gameObject.transform.localScale;
		testObject.transform.localPosition=new Vector3(0.0f,0.0f,0.0f);
		testObject.transform.localRotation=Quaternion.identity;
		testObject.AddComponent<MeshCollider>();
		//testObject.AddComponent<MeshRenderer>();
		testObject.GetComponent<MeshCollider>().sharedMesh=combinedMesh;
		//testObject.GetComponent<MeshFilter>().
		//testObject.GetComponent<MeshRenderer>().material=uniqueMatList[num];
		testObject.layer=LayerMask.NameToLayer("Default");
		testObject.tag = "AutoGeneratedPhys";
		//flag the object as static lightmapping always
		
		StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(testObject);
		
		//TODO, stati flags ignored for now
		//if(meshTag.staticLightMap)
		//{
		//	staticFlags |= StaticEditorFlags.LightmapStatic;
		//}
		
		//if(meshTag.staticBatch)
		//{
		//	staticFlags |= StaticEditorFlags.BatchingStatic;
		//}
		
		GameObjectUtility.SetStaticEditorFlags(testObject, staticFlags);
		//---
		Log( LogMode.Objects, "verts=:"+combinedMesh.vertexCount);
		AssetDatabase.CreateAsset(combinedMesh, AssetDatabase.GenerateUniqueAssetPath(exportFolderPath+"/"+"_Combined"+gameObject.name+".asset"));
		AssetDatabase.SaveAssets();
	}
	public void DeleteExportTags()
	{
		ExportTag[] exportTagObjects = GameObject.FindObjectsOfType<ExportTag>();//.FindGameObjectsWithTag("AutoPhysics");
		for (int num=0; num<exportTagObjects.Length;num++)
		{ 
			GameObject.DestroyImmediate(exportTagObjects[num].gameObject);
		}

	}
	public void DeleteExistingBuiltReflections()
	{
		MeshCollider[] autoPhysicsObjects = GameObject.FindObjectsOfType<MeshCollider>();//.FindGameObjectsWithTag("AutoPhysics");
		for (int num=0; num<autoPhysicsObjects.Length;num++)
		{ 
			if (autoPhysicsObjects[num].gameObject.name.Contains("_CombinedReflections"))
			{
				GameObject.DestroyImmediate(autoPhysicsObjects[num].gameObject);
			}
		}

		//delete old data
		DestroyAllObjectsWithTag("AutoGeneratedRef");

	}
	public void DestroyAllObjectsWithTag(string tag)
	{
		GameObject[] allGameObjects = GameObject.FindGameObjectsWithTag(tag);
		foreach (GameObject gameObject in allGameObjects)
		{
			GameObject.DestroyImmediate(gameObject);
		}
	}
	public void BuildReflectionData()
	{


		string exportFolderPath;
		_saveSceneName = CreateReflectionsFolder( EditorApplication.currentScene, out exportFolderPath );
		
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		foreach (GameObject gameObject in allGameObjects)
		{
			generatedMeshesInLastBatch.Clear();
			//for each mesh, go through all children (only direct children) and merge their meshes into an uber mesh
			//see if the object is tagged!
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			if (!meshTag)
			{
				// Not tagged, skip this object.
				continue;
			}
			if (!meshTag.ReflectionData)
			{
				continue;
			}
			//foundAnyCombineTags=true;
			
			if( meshTag.meshTag!=MeshCombineTag.MeshTag.CombineAllBelow)
			{
				// Don't combine children.
				continue;
			}
			int hits = 0;
			TestAndSplitMultiMaterialMeshes(gameObject.transform);
			CombineObjectChildren(gameObject,exportFolderPath,"Reflection", ref hits);
			DestroyTempSplitMeshes();
			//now we have made them we need to put them in the correct layer and mirror them!
			//make a transform to mirror the children about
			GameObject reflectedObject=new GameObject("Auto_Reflection");
			reflectedObject.transform.position = Vector3.zero;
			reflectedObject.transform.localScale = new Vector3(1.0f,-1.0f,1.0f);
			reflectedObject.transform.parent = gameObject.transform;
			reflectedObject.tag = "AutoGeneratedRef";
			for (int i = 0; i < generatedMeshesInLastBatch.Count; i++)
			{ 
				//unflag lightmap!
				//check that the bounding box is not entirely underwater
				//Debug.Log("Bounds max"+generatedMeshesInLastBatch[i].name+" = "+generatedMeshesInLastBatch[i].renderer.bounds.max);
				//Debug.Log("Bounds min"+generatedMeshesInLastBatch[i].name+" = "+generatedMeshesInLastBatch[i].renderer.bounds.min);
				if (generatedMeshesInLastBatch[i].renderer.bounds.max.y<0.05f)
				{
					Debug.Log("removing unwanted reflection: "+ generatedMeshesInLastBatch[i].name);
					GameObject.DestroyImmediate(generatedMeshesInLastBatch[i].gameObject);
					generatedMeshesInLastBatch[i] = null;

					continue;
				}

				StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags( generatedMeshesInLastBatch[i].gameObject );
				staticFlags &= ~StaticEditorFlags.LightmapStatic;
				GameObjectUtility.SetStaticEditorFlags(generatedMeshesInLastBatch[i].gameObject, staticFlags);

				generatedMeshesInLastBatch[i].gameObject.layer = LayerMask.NameToLayer("Reflections");  
				generatedMeshesInLastBatch[i].gameObject.tag = "AutoGeneratedRef";
				Vector3 origLocalScale = generatedMeshesInLastBatch[i].gameObject.transform.localScale;
				generatedMeshesInLastBatch[i].gameObject.transform.parent = reflectedObject.transform;
				generatedMeshesInLastBatch[i].gameObject.transform.localScale = origLocalScale;

				string matName = generatedMeshesInLastBatch[i].gameObject.GetComponent<MeshRenderer>().sharedMaterial.name;
				//swap to a reflected version of the material (if there is one)

				Material reflectedMat = AssetDatabase.LoadAssetAtPath("Assets/Materials/"+matName+"Reflection"+".mat",typeof(Material)) as Material;//Resources.Load("Content/Whitebox/Normal_Mat") as Material;
				if (reflectedMat)
				{
					generatedMeshesInLastBatch[i].gameObject.GetComponent<MeshRenderer>().sharedMaterial = reflectedMat;
				}

			}
			 
		} 
		
		//EditorApplication.SaveScene (_saveSceneName);
	}
	//--------
	public void ClearAllGenerated()
	{

		DeleteExistingBuiltRippleData();
		DeleteExistingBuiltReflections();
		DeleteExistingBuiltPhysics();
		DeleteExistingBuiltMoveBlockers();
		DestroyAllObjectsWithTag("AutoGeneratedVis");


		//del physics models

		DeleteCombinedFolder( EditorApplication.currentScene );
		DeleteReflectionsFolder( EditorApplication.currentScene );
		DeleteRipplesFolder( EditorApplication.currentScene );
		DeletePhysicsFolder( EditorApplication.currentScene );

	}
	bool CheckForCombineTagOnParent(Transform current)
	{
		Transform parent = current.parent;
		if (parent!= null && parent.GetComponent<MeshCombineTag>()!=null)
		{
			return true;
		}

		while (parent != null)
		{
			parent = parent.parent;

			if (parent!= null && parent.GetComponent<MeshCombineTag>()!=null)
			{
				return true;
			}
		}

		return false;
	}
	public void CombineAllTags()
	{
		AssetDatabase.StartAssetEditing();
		DestroyAllObjectsWithTag("AutoGeneratedVis");
		//delete all generated things of normal type
		DeleteCombinedFolder( EditorApplication.currentScene );

		MeshCombineTag[] allTags = GameObject.FindObjectsOfType<MeshCombineTag>() as MeshCombineTag[];
		for (int i = 0; i < allTags.Length; i++)
		{
			if (allTags[i].meshTag == MeshCombineTag.MeshTag.CombineAllBelow)
			{
				//if any parent has a combine tag, skip this object
				if (CheckForCombineTagOnParent(allTags[i].transform))
				{
					continue;
				}
				allTags[i].Generate();
			}
		}
		AssetDatabase.StopAssetEditing();
		AssetDatabase.SaveAssets(); 

		
	}
	public void ShowAllCombinedSource()
	{
		
		MeshCombineTag[] allTags = GameObject.FindObjectsOfType<MeshCombineTag>() as MeshCombineTag[];
		for (int i = 0; i < allTags.Length; i++)
		{
			if (allTags[i].meshTag == MeshCombineTag.MeshTag.CombineAllBelow)
			{
				//if any parent has a combine tag, skip this object
				if (CheckForCombineTagOnParent(allTags[i].transform))
				{
					continue;
				}
				allTags[i].ShowSource();
			}
		}


	}
	public void ShowAllCombined()
	{

		MeshCombineTag[] allTags = GameObject.FindObjectsOfType<MeshCombineTag>() as MeshCombineTag[];
		for (int i = 0; i < allTags.Length; i++)
		{
			if (allTags[i].meshTag == MeshCombineTag.MeshTag.CombineAllBelow)
			{
				//if any parent has a combine tag, skip this object
				if (CheckForCombineTagOnParent(allTags[i].transform))
				{
					continue;
				}
				allTags[i].ShowCombined();
			}
		}

		
	}
	public void DeleteExistingBuiltRippleData()
	{
		//MeshCollider[] autoPhysicsObjects = GameObject.FindObjectsOfType<MeshCollider>();//.FindGameObjectsWithTag("AutoPhysics");
		//for (int num=0; num<autoPhysicsObjects.Length;num++)
		//{ 
		//	if (autoPhysicsObjects[num].gameObject.name.Contains("_CombinedReflections"))
		//	{
		//		GameObject.DestroyImmediate(autoPhysicsObjects[num].gameObject);
		//	}
		//}

		while (GameObject.Find("Auto_Ripple"))
		{
			GameObject go = GameObject.Find("Auto_Ripple"); 
			GameObject.DestroyImmediate(go);
		}

		//delete old data
		DestroyAllObjectsWithTag("AutoGeneratedDepth");

		
	}
	public void DeleteExistingBuiltMoveBlockers()
	{

		
		while (GameObject.Find("Auto_Blocker"))
		{
			GameObject go = GameObject.Find("Auto_Blocker"); 
			GameObject.DestroyImmediate(go);
		}
		
		//delete old data
		DestroyAllObjectsWithTag("AutoGeneratedMoveBlocker");
		
		
	}
	public void BuildRippleData()
	{
		string exportFolderPath;
		_saveSceneName = CreateRipplesFolder( EditorApplication.currentScene, out exportFolderPath );
		
		GameObject[] allGameObjects=Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		foreach (GameObject gameObject in allGameObjects)
		{
			generatedMeshesInLastBatch.Clear();

			//for each mesh, go through all children (only direct children) and merge their meshes into an uber mesh
			//see if the object is tagged!
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			if (!meshTag)
			{
				// Not tagged, skip this object.
				continue;
			}
			if (!meshTag.RippleData)
			{
				continue;
			}

			//foundAnyCombineTags=true;
			
			if( meshTag.meshTag!=MeshCombineTag.MeshTag.CombineAllBelow)
			{
				// Don't combine children.
				continue;
			}
			int hits = 0;

			TestAndSplitMultiMaterialMeshes(gameObject.transform);

			CombineObjectChildren(gameObject,exportFolderPath,"Ripples", ref hits);

			DestroyTempSplitMeshes();
			//now we have made them we need to put them in the correct layer and mirror them!
			//make a transform to mirror the children about
			GameObject rippleObject=new GameObject("Auto_Ripple");
			rippleObject.tag = "AutoGeneratedDepth";
			rippleObject.transform.position = Vector3.zero;
			rippleObject.transform.localScale = new Vector3(1.0f,1.0f,1.0f);


			//try to find the parent location to put this geometry


			rippleObject.transform.parent = gameObject.transform;
			for (int i = 0; i < generatedMeshesInLastBatch.Count; i++)
			{ 
				StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags( generatedMeshesInLastBatch[i].gameObject );
				staticFlags &= ~StaticEditorFlags.LightmapStatic;
				GameObjectUtility.SetStaticEditorFlags(generatedMeshesInLastBatch[i].gameObject, staticFlags);

				generatedMeshesInLastBatch[i].gameObject.layer = LayerMask.NameToLayer("DepthInfo");  
				generatedMeshesInLastBatch[i].gameObject.tag = "AutoGeneratedDepth";
				//swap to material to depthcull
				MeshRenderer renderer = generatedMeshesInLastBatch[i].gameObject.GetComponent<MeshRenderer>();
				if (renderer)
				{
					//renderer.sharedMaterial = AssetDatabase.;
					renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath("Assets/Materials/DepthCull.mat",typeof(Material)) as Material;//Resources.Load("Content/Whitebox/Normal_Mat") as Material;

				}
				generatedMeshesInLastBatch[i].gameObject.transform.parent = rippleObject.transform;

				//Resources.
			}
			
		} 
		
		//EditorApplication.SaveScene (_saveSceneName);
	}


	public void CombineMoveBlockers()
	{
		string exportFolderPath;

		combinedMeshesInLastBatch.Clear();
		generatedMeshesInLastBatch.Clear();

		_saveSceneName = CreateMoveBlockersFolder( EditorApplication.currentScene, out exportFolderPath );

		Transform[] allGameObjects=GameObject.FindObjectsOfType<Transform>() as Transform[];// Object.FindObjectsOfType(typeof(GameObject)) as GameObject[];
		
		int hits = 0;
		foreach (Transform gameObject in allGameObjects)
		{
			//for each mesh, go through all children (only direct children) and merge their meshes into an uber mesh
			//see if the object is tagged!
			MeshCombineTag meshTag=gameObject.GetComponent<MeshCombineTag>();
			if (!meshTag)
			{
				// Not tagged, skip this object.
				continue;
			}
			
			//foundAnyCombineTags=true;
			
			if( meshTag.meshTag!=MeshCombineTag.MeshTag.CombineAllBelow)
			{
				// Don't combine children.
				continue;
			}

			CombineObjectChildren( gameObject.gameObject, exportFolderPath,"", ref hits,LayerMask.NameToLayer("MoveBlocker") ,true);
			//AutoGeneratedMoveBlocker

			GameObject blockerObject = null;
			if (generatedMeshesInLastBatch.Count>0)
			{
				Debug.Log("new blockers count:"+generatedMeshesInLastBatch.Count);
				blockerObject=new GameObject("Auto_Blocker");
				blockerObject.tag = "AutoGeneratedMoveBlocker";
				blockerObject.layer = LayerMask.NameToLayer("MoveBlocker");
				blockerObject.transform.position = Vector3.zero;
				blockerObject.transform.localScale = new Vector3(1.0f,1.0f,1.0f);
				blockerObject.transform.parent = gameObject.transform;

			}			
			//try to find the parent location to put this geometry			
			
			for (int i = 0; i < generatedMeshesInLastBatch.Count; i++)
			{ 
				StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags( generatedMeshesInLastBatch[i].gameObject );
				staticFlags &= ~StaticEditorFlags.LightmapStatic;
				GameObjectUtility.SetStaticEditorFlags(generatedMeshesInLastBatch[i].gameObject, staticFlags);
				
				generatedMeshesInLastBatch[i].gameObject.layer = LayerMask.NameToLayer("MoveBlocker");  
				generatedMeshesInLastBatch[i].gameObject.tag = "AutoGeneratedMoveBlocker";
				MeshCollider collider = generatedMeshesInLastBatch[i].gameObject.AddComponent<MeshCollider>();
				collider.sharedMesh = generatedMeshesInLastBatch[i].gameObject.GetComponent<MeshFilter>().sharedMesh;

				generatedMeshesInLastBatch[i].gameObject.transform.parent = blockerObject.transform;

			}
			for (int i = 0; i < combinedMeshesInLastBatch.Count; i++)
			{
				combinedMeshesInLastBatch[i].gameObject.tag = "SourceMoveBlocker";
			}
			combinedMeshesInLastBatch.Clear();
			generatedMeshesInLastBatch.Clear();
		}

	}
}
#endif
