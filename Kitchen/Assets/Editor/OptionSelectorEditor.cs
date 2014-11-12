using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(OptionSelector))]
public class OptionSelectorEditor : Editor
{
	public override void OnInspectorGUI()
	{
		OptionSelector selector = target as OptionSelector;

		selector.cycleOptionKey = (KeyCode)EditorGUILayout.EnumPopup( "Cycle Key", selector.cycleOptionKey );
		selector.cycleOnClick = EditorGUILayout.Toggle( "Cycle On Click", selector.cycleOnClick );

		selector.optionText = (TextMesh)EditorGUILayout.ObjectField( selector.optionText, typeof(TextMesh), true );

		int count = selector.options.Count;
		string[] texts = new string[count];
		for( int i=0 ; i<count ; i++ )
		{
			texts[i] = selector.options[i].name;
		}
		int newOption = GUILayout.SelectionGrid( selector.option, texts, 2 );
		if( newOption != selector.option )
		{
			selector.option = newOption;
			EditorUtility.SetDirty( selector );
		}
	}
}

