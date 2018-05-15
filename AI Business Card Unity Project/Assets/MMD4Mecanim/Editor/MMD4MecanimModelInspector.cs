//#define _MMD4MECANIM_DEBUG_DEFAULTINSPECTOR

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MMDModel = MMD4MecanimImporter.MMDModel;

[CustomEditor(typeof(MMD4MecanimModel))]
public class MMD4MecanimModelInspector : Editor
{
	private static readonly string[] toolbarTitles = new string[] {
		"Model", "Bone", "IK", "Morph", "Anim", "Physics",
	};

	private bool _initialized;
#if _MMD4MECANIM_DEBUG_DEFAULTINSPECTOR
	private bool _defaultInspector;
#endif

	public override void OnInspectorGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;

		if( string.IsNullOrEmpty( AssetDatabase.GetAssetPath( model ) ) ) { // Bugfix: Broken prefab.
			model.InitializeOnEditor();
		}

		_Initialize();

#if _MMD4MECANIM_DEBUG_DEFAULTINSPECTOR
		_defaultInspector = GUILayout.Toggle( _defaultInspector, "DefaultInspector" );
		if( _defaultInspector ) {
			DrawDefaultInspector();
			return;
		}
#endif

		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		model.editorViewPage = (MMD4MecanimModel.EditorViewPage)GUILayout.Toolbar( (int)model.editorViewPage, toolbarTitles );
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		
		switch( model.editorViewPage ) {
		case MMD4MecanimModel.EditorViewPage.Model:
			_DrawModelGUI();
			break;
		case MMD4MecanimModel.EditorViewPage.Bone:
			_DrawBoneGUI();
			break;
		case MMD4MecanimModel.EditorViewPage.IK:
			_DrawIKGUI();
			break;
		case MMD4MecanimModel.EditorViewPage.Morph:
			_DrawMorphGUI();
			break;
		case MMD4MecanimModel.EditorViewPage.Anim:
			_DrawAnimGUI();
			break;
		case MMD4MecanimModel.EditorViewPage.Physics:
			_DrawPhysicsGUI();
			break;
		}
	}

	private void _Initialize()
	{
		if( _initialized ) {
			return;
		}
		
		_initialized = true;
		
		MMD4MecanimModel model = this.target as MMD4MecanimModel;

		Mesh mesh = model.defaultMesh;
		if( mesh == null ) {
			Debug.LogWarning( "defaultMesh is null." );
			return;
		}
		
		string fbxAssetPath = AssetDatabase.GetAssetPath( mesh );
		
		if( model.modelFile == null ) {
			if( !string.IsNullOrEmpty( fbxAssetPath ) ) {
				string modelAssetPath = System.IO.Path.GetDirectoryName( fbxAssetPath ) + "/"
					+ System.IO.Path.GetFileNameWithoutExtension( fbxAssetPath )
					+ ".model.bytes";
				
				model.modelFile = AssetDatabase.LoadAssetAtPath( modelAssetPath, typeof(TextAsset) ) as TextAsset;
			}
		}
		if( model.isSkinning ) {
			if( model.indexFile == null ) {
				if( !string.IsNullOrEmpty( fbxAssetPath ) ) {
					string indexAssetPath = System.IO.Path.GetDirectoryName( fbxAssetPath ) + "/"
						+ System.IO.Path.GetFileNameWithoutExtension( fbxAssetPath )
						+ ".index.bytes";
					
					model.indexFile = AssetDatabase.LoadAssetAtPath( indexAssetPath, typeof(TextAsset) ) as TextAsset;
				}
			}
		}
	}

	private void _DrawModelGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;
		
		model.initializeOnAwake = EditorGUILayout.Toggle( "Initialize On Awake", model.initializeOnAwake );
		model.postfixRenderQueue = EditorGUILayout.Toggle( "Postfix Render Queue", model.postfixRenderQueue );
		model.updateWhenOffscreen = EditorGUILayout.Toggle( "Update When Offscreen", model.updateWhenOffscreen );

		{
			TextAsset modelFile = model.modelFile;
			modelFile = (TextAsset)EditorGUILayout.ObjectField( "Model File", (Object)modelFile, typeof(TextAsset), false );
			if( modelFile != null ) {
				if( !AssetDatabase.GetAssetPath( modelFile ).ToLower().EndsWith( ".model.bytes" ) ) {
					modelFile = null;
				} else {
					model.modelFile = modelFile;
				}
			} else {
				model.modelFile = modelFile;
			}
		}
		
		{
			TextAsset indexFile = model.indexFile;
			indexFile = (TextAsset)EditorGUILayout.ObjectField( "Index File", (Object)indexFile, typeof(TextAsset), false );
			if( indexFile != null ) {
				if( !AssetDatabase.GetAssetPath( indexFile ).ToLower().EndsWith( ".index.bytes" ) ) {
					indexFile = null;
				} else {
					model.indexFile = indexFile;
				}
			} else {
				model.indexFile = indexFile;
			}
		}
		
		model.audioSource = (AudioSource)EditorGUILayout.ObjectField( "Audio Source", (Object)model.audioSource, typeof(AudioSource), true );
		
		model.physicsEngine = (MMD4MecanimModel.PhysicsEngine)EditorGUILayout.EnumPopup( "Physics Engine", (System.Enum)model.physicsEngine );
	}

	private void _DrawBoneGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;
		
		// DisplayFrame
		
		MMD4MecanimEditorCommon.LookLikeInspector();
		int boneListLength = 0;
		if( model.boneList != null ) {
			boneListLength = model.boneList.Length;
		}

		EditorGUILayout.Separator();
		
		model.boneInherenceEnabled			= EditorGUILayout.Toggle( "BoneInherenceEnabled", model.boneInherenceEnabled );
		model.boneInherenceEnabledGeneric	= EditorGUILayout.Toggle( "BoneInherenceEnabledGeneric", model.boneInherenceEnabledGeneric );

		EditorGUILayout.Separator();

		model.pphEnabled			= EditorGUILayout.Toggle( "PPHEnabled", model.pphEnabled );
		model.pphEnabledNoAnimation	= EditorGUILayout.Toggle( "PPHEnabledNoAnimation", model.pphEnabledNoAnimation );

		EditorGUILayout.Separator();

		model.pphShoulderEnabled	= EditorGUILayout.Toggle( "PPHShoulderEnabled", model.pphShoulderEnabled );
		model.pphShoulderFixRate	= EditorGUILayout.Slider( "PPHShoulderFixRate", model.pphShoulderFixRate, 0.0f, 1.0f ); 

		EditorGUILayout.Separator();

		EditorGUILayout.TextField( "Size", boneListLength.ToString() );
		for( int i = 0; i < boneListLength; ++i ) {
			string name = i.ToString();
			if( model.modelData != null && model.modelData.boneDataList != null && i < model.modelData.boneDataList.Length ) {
				name = name + "." + model.modelData.boneDataList[i].nameJp;
			}
			GameObject boneGameObject = (model.boneList[i] != null) ? model.boneList[i].gameObject : null;
			EditorGUILayout.ObjectField( name, (Object)boneGameObject, typeof(GameObject), true );
		}
	}

	private void _DrawIKGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;
		
		// DisplayFrame
		
		MMD4MecanimEditorCommon.LookLikeInspector();
		int ikListLength = 0;
		if( model.ikList != null ) {
			ikListLength = model.ikList.Length;
		}
		
		EditorGUILayout.Separator();
		
		model.ikEnabled = EditorGUILayout.Toggle( "IKEnabled", model.ikEnabled );
		
		EditorGUILayout.Separator();
		
		EditorGUILayout.TextField( "Size", ikListLength.ToString() );
		for( int i = 0; i < ikListLength; ++i ) {
			MMD4MecanimModel.IK ik = model.ikList[i];
			if( ik != null ) {
				string name = i.ToString();
				if( ik.destBone != null && ik.destBone.boneData != null ) {
					if( ik.destBone.boneData.nameJp != null ) {
						name = name + "." + ik.destBone.boneData.nameJp;
					}
				}

				EditorGUILayout.BeginHorizontal();
				ik.ikEnabled = GUILayout.Toggle(ik.ikEnabled, name);
				GUILayout.FlexibleSpace();
				name = "";
				GameObject boneGameObject = (ik.destBone != null) ? ik.destBone.gameObject : null;
				EditorGUILayout.ObjectField( name, (Object)boneGameObject, typeof(GameObject), true );
				EditorGUILayout.EndHorizontal();
			}
		}
	}
	
	private void _DrawMorphGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;

		bool updatedAnything = false;
		if( model.modelData != null && model.modelData.morphDataList != null ) {
			for( int catIndex = 1; catIndex < 5; ++catIndex ) {
				MMD4MecanimData.MorphCategory morphCategory = (MMD4MecanimData.MorphCategory)catIndex;
				bool isVisible = (model.editorViewMorphBits & (1 << (catIndex - 1))) != 0;

				isVisible = GUILayout.Toggle( isVisible, morphCategory.ToString() );
				if( isVisible ) {
					model.editorViewMorphBits |= unchecked((byte)(1 << (catIndex - 1)));
				} else {
					model.editorViewMorphBits &= unchecked((byte)~(1 << (catIndex - 1)));
				}
				if( isVisible ) {
					for( int morphIndex = 0; morphIndex < model.modelData.morphDataList.Length; ++morphIndex ) {
						if( model.modelData.morphDataList[morphIndex].morphCategory == morphCategory ) {
							string name = model.modelData.morphDataList[morphIndex].nameJp;
							if( model.morphList != null && (uint)morphIndex < model.morphList.Length ) {
								float weight = model.morphList[morphIndex].weight;
								model.morphList[morphIndex].weight = EditorGUILayout.Slider( name, model.morphList[morphIndex].weight, 0.0f, 1.0f ); 
								updatedAnything |= (weight != model.morphList[morphIndex].weight);
							}
						}
					}
				}
			}
		} else {
			if( model.morphList != null ) {
				foreach( MMD4MecanimModel.Morph morph in model.morphList ) {
					float weight = morph.weight;
					morph.weight = EditorGUILayout.Slider( morph.name, morph.weight, 0.0f, 1.0f );
					updatedAnything |= (weight != morph.weight);
				}
			}
		}

		if( updatedAnything ) {
			model.ForceUpdateMorph();
		}
	}

	private void _DrawAnimGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;

		model.animEnabled = GUILayout.Toggle( model.animEnabled, "Enabled" );
		
		GUI.enabled = model.animEnabled;

		model.animSyncToAudio = GUILayout.Toggle( model.animSyncToAudio, "Sync To Audio" );
		
		if( model.animList == null ) {
			model.animList = new MMD4MecanimModel.Anim[0];
		}
		
		//EditorGUILayout.Separator();
		if( model.animList != null ) {
			if( model.animList.Length > 0 ) {
				GUILayout.Label( "Animations", EditorStyles.boldLabel );
			}
			for( int animIndex = 0; animIndex < model.animList.Length; ) {
				MMD4MecanimModel.Anim anim = model.animList[animIndex];
				TextAsset animFile = anim.animFile;
				EditorGUILayout.BeginHorizontal();
				bool isRemove = GUILayout.Button("-", EditorStyles.miniButton, GUILayout.ExpandWidth(false) );
				animFile = (TextAsset)EditorGUILayout.ObjectField( "Anim File", (Object)animFile, typeof(TextAsset), false );
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(26.0f);
				anim.animatorStateName = EditorGUILayout.TextField( "Animator State Name", anim.animatorStateName );
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(26.0f);
				anim.audioClip = (AudioClip)EditorGUILayout.ObjectField( "Audio Clip", (AudioClip)anim.audioClip, typeof(AudioClip), false );
				EditorGUILayout.EndHorizontal();
				if( animFile != null ) {
					if( !AssetDatabase.GetAssetPath( animFile ).ToLower().EndsWith( ".anim.bytes" ) ) {
						animFile = null;
					} else {
						if( anim.animFile != animFile ) {
							anim.animFile = animFile;
							anim.animatorStateName = "Base Layer." + System.IO.Path.GetFileNameWithoutExtension( anim.animFile.name ) + ".vmd";
						}
					}
				} else {
					isRemove = true;
					anim.animFile = null;
					anim.animatorStateName = "";
				}
				if( isRemove ) {
					for( int i = animIndex; i + 1 < model.animList.Length; ++i ) {
						model.animList[i] = model.animList[i + 1];
					}
					System.Array.Resize( ref model.animList, model.animList.Length - 1 );
				} else {
					++animIndex;
				}
			}
		}

		EditorGUILayout.Separator();
		
		{
			GUILayout.Label( "Add Animation", EditorStyles.boldLabel );
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space(26.0f);
			TextAsset animFile = (TextAsset)EditorGUILayout.ObjectField( "Anim File", (Object)null, typeof(TextAsset), false );
			EditorGUILayout.EndHorizontal();
			if( animFile != null ) {
				if( !AssetDatabase.GetAssetPath( animFile ).ToLower().EndsWith( ".anim.bytes" ) ) {
					Debug.LogWarning( System.IO.Path.GetExtension( AssetDatabase.GetAssetPath( animFile ) ).ToLower() );
					animFile = null;
				} else {
					MMD4MecanimModel.Anim anim = new MMD4MecanimModel.Anim();
					anim.animFile = animFile;
					anim.animatorStateName = "Base Layer." + System.IO.Path.GetFileNameWithoutExtension( anim.animFile.name ) + ".vmd";
					if( model.animList == null ) {
						model.animList = new MMD4MecanimModel.Anim[1];
						model.animList[0] = anim;
					} else {
						int animIndex = model.animList.Length;
						System.Array.Resize( ref model.animList, animIndex + 1 );
						model.animList[animIndex] = anim;
					}
				}
			}
		}
	}

	private void _DrawPhysicsGUI()
	{
		MMD4MecanimModel model = this.target as MMD4MecanimModel;
		
		GUILayout.Label( "Model", EditorStyles.boldLabel );
		model.physicsEngine = (MMD4MecanimModel.PhysicsEngine)EditorGUILayout.EnumPopup( "Physics Engine", (System.Enum)model.physicsEngine );
		EditorGUILayout.Separator();
		
		GUI.enabled = (model.physicsEngine == MMD4MecanimModel.PhysicsEngine.BulletPhysics);
		GUILayout.Label( "Bullet Physics", EditorStyles.boldLabel );
		if( model.bulletPhysics != null ) {
			model.bulletPhysics.joinLocalWorld = EditorGUILayout.Toggle( "Join Local World", model.bulletPhysics.joinLocalWorld );
			model.bulletPhysics.useOriginalScale = EditorGUILayout.Toggle( "Use Original Scale", model.bulletPhysics.useOriginalScale );
		}

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space( 20.0f );
		GUILayout.Label( "Reset Time Property", EditorStyles.boldLabel );
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space( 20.0f );
		model.bulletPhysics.useCustomResetTime = EditorGUILayout.Toggle( "Use Custom Reset Time", model.bulletPhysics.useCustomResetTime );
		EditorGUILayout.EndHorizontal();

		GUI.enabled = (model.physicsEngine == MMD4MecanimModel.PhysicsEngine.BulletPhysics) && model.bulletPhysics.useCustomResetTime;

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space( 20.0f );
		model.bulletPhysics.resetMorphTime = EditorGUILayout.FloatField( "Reset Morph Time", model.bulletPhysics.resetMorphTime );
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space( 20.0f );
		model.bulletPhysics.resetWaitTime = EditorGUILayout.FloatField( "Reset Wait Time", model.bulletPhysics.resetWaitTime );
		EditorGUILayout.EndHorizontal();

		GUI.enabled = (model.physicsEngine == MMD4MecanimModel.PhysicsEngine.BulletPhysics);

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space( 20.0f );
		GUILayout.Label( "World Property", EditorStyles.boldLabel );
		EditorGUILayout.EndHorizontal();

		if( model.bulletPhysics.worldProperty == null ) {
			model.bulletPhysics.worldProperty = new MMD4MecanimBulletPhysics.WorldProperty();
		}

		if( model.bulletPhysics.worldProperty != null ) {
			var worldProperty = model.bulletPhysics.worldProperty;
			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			worldProperty.framePerSecond = EditorGUILayout.IntField( "Frame Per Second", worldProperty.framePerSecond );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			worldProperty.gravityScale = EditorGUILayout.FloatField( "Gravity Scale", worldProperty.gravityScale );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			worldProperty.vertexScale = EditorGUILayout.FloatField( "Vertex Scale", worldProperty.vertexScale );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			worldProperty.importScale = EditorGUILayout.FloatField( "Import Scale", worldProperty.importScale );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			worldProperty.worldSolverInfoNumIterations = EditorGUILayout.IntField( "Iterations", worldProperty.worldSolverInfoNumIterations );
			EditorGUILayout.EndHorizontal();
		}

		GUI.enabled = (model.physicsEngine == MMD4MecanimModel.PhysicsEngine.BulletPhysics);

		EditorGUILayout.BeginHorizontal();
		GUILayout.Space( 20.0f );
		GUILayout.Label( "Rigid Body", EditorStyles.boldLabel );
		EditorGUILayout.EndHorizontal();

		if( model.bulletPhysics.mmdModelRigidBodyProperty == null ) {
			model.bulletPhysics.mmdModelRigidBodyProperty = new MMD4MecanimBulletPhysics.MMDModelRigidBodyProperty();
		}

		if( model.bulletPhysics.mmdModelRigidBodyProperty != null ) {
			var ridigBodyProperty = model.bulletPhysics.mmdModelRigidBodyProperty;

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			ridigBodyProperty.isAdditionalDamping = EditorGUILayout.Toggle( "AdditionalDamping", ridigBodyProperty.isAdditionalDamping );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			ridigBodyProperty.mass = EditorGUILayout.FloatField( "Mass", ridigBodyProperty.mass );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			ridigBodyProperty.linearDamping = EditorGUILayout.FloatField( "LinearDamping", ridigBodyProperty.linearDamping );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			ridigBodyProperty.angularDamping = EditorGUILayout.FloatField( "AngularDamping", ridigBodyProperty.angularDamping );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			ridigBodyProperty.restitution = EditorGUILayout.FloatField( "Restitution", ridigBodyProperty.restitution );
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.Space( 20.0f );
			ridigBodyProperty.friction = EditorGUILayout.FloatField( "Friction", ridigBodyProperty.friction );
			EditorGUILayout.EndHorizontal();
		}
	}
}
