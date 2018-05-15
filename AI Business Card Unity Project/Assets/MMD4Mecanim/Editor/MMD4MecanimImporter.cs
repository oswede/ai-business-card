using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using IndexData = MMD4MecanimData.IndexData;

public partial class MMD4MecanimImporter : ScriptableObject
{
	private static readonly string[] toolbarTitles = new string[] {
		"PMX2FBX", "Material", "Rig", "Animations",
	};

	public enum EditorViewPage
	{
		PMX2FBX,
		Material,
		Rig,
		Animations,
	}

	public class PMX2FBXProperty
	{
		public bool			viewAdvancedGlobalSettings;
		public bool			viewAdvancedBulletPhysics;
		
		public Object		pmxAsset;
		public List<Object>	vmdAssetList = new List<Object>();
	}

	public GameObject		fbxAsset;
	public string			fbxAssetPath;

	[System.NonSerialized]
	public MMDModel			mmdModel;
	[System.NonSerialized]
	public System.DateTime	mmdModelLastWriteTime;

	[System.NonSerialized]
	public TextAsset		indexAsset;
	[System.NonSerialized]
	public IndexData		indexData;

	public EditorViewPage	editorViewPage;

	[System.NonSerialized]
	public PMX2FBXProperty	pmx2fbxProperty;
	[System.NonSerialized]
	public PMX2FBXConfig	pmx2fbxConfig;
	[System.NonSerialized]
	public bool				pmx2fbxConfigWritten;

	private bool			_prepareDependencyAtLeastOnce;
	private volatile bool	_forceCheckChanged;

	void OnEnable()
	{
	}
	
	void OnDisable()
	{
	}

	public void OnInspectorGUI()
	{
		if( !Application.isPlaying ) {
			if( !_prepareDependencyAtLeastOnce ) {
				_prepareDependencyAtLeastOnce = true;
				_PrepareDependency();
			}
		}

		if( !Setup() ) {
			return;
		}

		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		this.editorViewPage = (EditorViewPage)GUILayout.Toolbar( (int)this.editorViewPage, toolbarTitles );
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		EditorGUILayout.Separator();
		
		switch( this.editorViewPage ) {
		case EditorViewPage.PMX2FBX:
			_OnInspectorGUI_PMX2FBX();
			break;
		case EditorViewPage.Material:
			_OnInspectorGUI_Material();
			break;
		case EditorViewPage.Rig:
			_OnInspectorGUI_Rig();
			break;
		case EditorViewPage.Animations:
			_OnInspectorGUI_Animations();
			break;
		}
	}

	void _OnInspectorGUI_ShowFBXField()
	{
		GameObject fbxAsset = this.fbxAsset;
		fbxAsset = EditorGUILayout.ObjectField( (Object)fbxAsset, typeof(GameObject), false ) as GameObject;
		if( fbxAsset != null && fbxAsset != this.fbxAsset ) {
			string fbxAssetPath = AssetDatabase.GetAssetPath( fbxAsset );
			if( !MMD4MecanimEditorCommon.IsExtensionFBX( fbxAssetPath ) ) {
				fbxAsset = null;
			} else {
				this.fbxAsset = fbxAsset;
				this.fbxAssetPath = fbxAssetPath;

				this.mmdModel = null;
				this.mmdModelLastWriteTime = new System.DateTime();
				this.indexAsset = null;
				this.indexData = null;
				PrepareDependency();
			}
		}
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	public static readonly string ScriptExtension = ".MMD4Mecanim.asset";

	private static MMD4MecanimImporter[] _cachedAllAssets = null;

	public static void SetDirtyCachedAllAssets()
	{
		_cachedAllAssets = null;
	}

	public static MMD4MecanimImporter[] GetAllAssets()
	{
		if( _cachedAllAssets != null ) {
			foreach( MMD4MecanimImporter asset in _cachedAllAssets ) {
				if( asset == null ) { // Already destroyed.
					_cachedAllAssets = null;
					break;
				}
			}
			if( _cachedAllAssets != null ) {
				return _cachedAllAssets;
			}
		}

		string[] assetPaths = AssetDatabase.GetAllAssetPaths();
		if( assetPaths != null ) {
			List<MMD4MecanimImporter> assets = new List<MMD4MecanimImporter>();
			foreach( string assetPath in assetPaths ) {
				if( assetPath.EndsWith( ScriptExtension ) ) {
					MMD4MecanimImporter asset = AssetDatabase.LoadAssetAtPath( assetPath, typeof(MMD4MecanimImporter) ) as MMD4MecanimImporter;
					if( asset != null ) {
						assets.Add( asset );
					}
				}
			}

			_cachedAllAssets = assets.ToArray();
			return _cachedAllAssets;
		}

		return null;
	}

	public static void CheckAndCreateAsset( string assetPath )
	{
		if( MMD4MecanimEditorCommon.IsExtensionPMDorPMX( assetPath ) ) { // ".pmx" or ".pmd"
			string importerAssetPath = MMD4MecanimEditorCommon.GetPathWithoutExtension( assetPath, 4 ) + ScriptExtension;
			importerAssetPath = importerAssetPath.Normalize(NormalizationForm.FormC); // for MAC
			if( !File.Exists( importerAssetPath ) ) {
				SetDirtyCachedAllAssets();
				MMD4MecanimImporter importer = ScriptableObject.CreateInstance< MMD4MecanimImporter >();
				AssetDatabase.CreateAsset( importer, importerAssetPath );
			}
		}
	}

	public static void ForceAllCheckAndCreateAssets( string[] assetPaths )
	{
		if( assetPaths != null ) {
			foreach( string assetPath in assetPaths ) {
				CheckAndCreateAsset( assetPath );
			}
		}
	}

	public static void ForceAllCheckAndCreateAssets()
	{
		ForceAllCheckAndCreateAssets( AssetDatabase.GetAllAssetPaths() );
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	public bool CheckChanged()
	{
		if( this.isProcessing ) {
			#if MMD4MECANIM_DEBUG
			Debug.LogWarning( "MMD4MecanimDebug: CheckChanged: Cancelled in processing." );
			#endif
			_forceCheckChanged = true;
			return false;
		}
		
		if( !Setup() ) {
			return true;
		}

		if( this.fbxAsset != null ) {
			string fbxAssetPath = AssetDatabase.GetAssetPath( this.fbxAsset );
			if( this.fbxAssetPath != fbxAssetPath ) {
				this.fbxAssetPath = fbxAssetPath;
				
				this.mmdModel = null;
				this.mmdModelLastWriteTime = new System.DateTime();
				this.indexAsset = null;
				this.indexData = null;
			}
		}

		PrepareDependency();
		return true;
	}

	public bool ForceCheckChanged()
	{
		if( this.isProcessing ) {
			return false;
		}
		if( !_forceCheckChanged ) {
			return true;
		}

		_forceCheckChanged = false;

		if( this.pmx2fbxConfig != null && this.pmx2fbxConfig.mmd4MecanimProperty != null ) {
			string fbxAssetPath = this.pmx2fbxConfig.mmd4MecanimProperty.fbxOutputPath;
			AssetDatabase.ImportAsset( fbxAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport );
		}

		CheckChanged();
		return true;
	}

	//--------------------------------------------------------------------------------------------------------------------------------------------

	public static void ForceAllCheckModelInScene()
	{
		List<Animator> assetAnimators = new List<Animator>();
		List<string> assetPaths = new List<string>();
		List<bool> assetIsSkinned = new List<bool>();

		Animator[] animators = GameObject.FindObjectsOfType( typeof(Animator) ) as Animator[];
		if( animators != null ) {
			foreach( Animator animator in animators ) {
				if( animator.gameObject.GetComponent< MMD4MecanimModel >() == null ) {
					bool added = false;
					SkinnedMeshRenderer skinnedMeshRenderer = MMD4MecanimCommon.GetSkinnedMeshRenderer( animator.gameObject );
					if( skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null ) {
						string assetPath = AssetDatabase.GetAssetPath( skinnedMeshRenderer.sharedMesh );
						if( !string.IsNullOrEmpty( assetPath ) ) {
							assetAnimators.Add( animator );
							assetPaths.Add( assetPath );
							assetIsSkinned.Add( true );
							added = true;
						}
					}
					if( !added ) {
						MeshRenderer meshRenderer = MMD4MecanimCommon.GetMeshRenderer( animator.gameObject );
						if( meshRenderer != null ) {
							MeshFilter meshFilter = meshRenderer.gameObject.GetComponent<MeshFilter>();
							if( meshFilter != null && meshFilter.sharedMesh != null ) {
								string assetPath = AssetDatabase.GetAssetPath( meshFilter.sharedMesh );
								if( !string.IsNullOrEmpty( assetPath ) ) {
									assetAnimators.Add( animator );
									assetPaths.Add( assetPath );
									assetIsSkinned.Add( false );
								}
							}
						}
					}
				}
			}
		}

		MMD4MecanimImporter[] importerAssets = GetAllAssets();
		if( importerAssets != null ) {
			foreach( MMD4MecanimImporter importerAsset in importerAssets ) {
				importerAsset._CheckModelInScene( assetAnimators, assetPaths, assetIsSkinned );
			}
		}
	}
	
	private string _GetScriptAssetPath()
	{
		return AssetDatabase.GetAssetPath( this );
	}
	
	private string _GetScriptAssetPathWithoutExtension()
	{
		return MMD4MecanimEditorCommon.GetPathWithoutExtension( _GetScriptAssetPath(), ScriptExtension.Length );
	}
	
	public static string GetPMX2FBXRootConfigPath()
	{
		Shader mmdlitShader = Shader.Find( "MMD4Mecanim/MMDLit" );
		if( mmdlitShader != null ) {
			string shaderAssetPath = AssetDatabase.GetAssetPath( mmdlitShader );
			string pmx2fbxConfigPath = Path.GetDirectoryName( Path.GetDirectoryName( shaderAssetPath ) )
										+ "/Editor/PMX2FBX/pmx2fbx.xml";
			
			if( File.Exists( pmx2fbxConfigPath ) ) {
				return pmx2fbxConfigPath;
			}
		}
		
		return null;
	}

	public static string GetPMX2FBXPath( bool useWineFlag )
	{
		Shader mmdlitShader = Shader.Find( "MMD4Mecanim/MMDLit" );
		if( mmdlitShader != null ) {
			string pmx2fbxExecutePath = "/Editor/PMX2FBX/pmx2fbx";
			if( Application.platform == RuntimePlatform.WindowsEditor || useWineFlag ) {
				pmx2fbxExecutePath = pmx2fbxExecutePath + ".exe";
			}
			
			string shaderAssetPath = AssetDatabase.GetAssetPath( mmdlitShader );
			string pmx2fbxPath = Path.GetDirectoryName( Path.GetDirectoryName( shaderAssetPath ) ) + pmx2fbxExecutePath;
			if( File.Exists( pmx2fbxPath ) ) {
				return pmx2fbxPath;
			}
		}
		
		return null;
	}

	public bool PrepareDependency()
	{
		if( this.isProcessing ) {
			return false;
		}

		_PrepareDependency();
		return true;
	}

	private void _PrepareDependency()
	{
		if( this.isProcessing ) {
			return;
		}
		
		if( !Setup() ) {
			return;
		}

		// Check FBX.
		if( this.fbxAsset == null && this.pmx2fbxConfig.mmd4MecanimProperty != null ) {
			string fbxAssetPath = this.pmx2fbxConfig.mmd4MecanimProperty.fbxOutputPath;
			if( File.Exists( fbxAssetPath ) ) {
				#if MMD4MECANIM_DEBUG
				Debug.Log( "Load fbxAssetPath:" + fbxAssetPath );
				#endif
				this.fbxAsset = AssetDatabase.LoadAssetAtPath( fbxAssetPath, typeof(GameObject) ) as GameObject;
				this.fbxAssetPath = fbxAssetPath;
			}
		}
		
		// Check MMDModel.
		if( this.fbxAsset != null ) {
			string mmdModelPath = GetMMDModelPath( this.fbxAssetPath );
			if( File.Exists( mmdModelPath ) ) {
				if( this.mmdModel == null || this.mmdModelLastWriteTime != MMD4MecanimEditorCommon.GetLastWriteTime( mmdModelPath ) ) {
					#if MMD4MECANIM_DEBUG
					Debug.Log( "Load mmdModelPath:" + mmdModelPath );
					#endif
					this.mmdModel = GetMMDModel( mmdModelPath );
					this.mmdModelLastWriteTime = MMD4MecanimEditorCommon.GetLastWriteTime( mmdModelPath );
				} else {
					// No changed.
				}
			} else {
				this.mmdModel = null;
				this.mmdModelLastWriteTime = new System.DateTime();
			}
		} else {
			this.mmdModel = null;
			this.mmdModelLastWriteTime = new System.DateTime();
		}
		
		// Check IndexData.
		if( this.fbxAsset != null ) {
			if( this.indexAsset == null || this.indexData == null ) {
				string indexAssetPath = GetIndexDataPath( this.fbxAssetPath );
				if( File.Exists( indexAssetPath ) ) {
					#if MMD4MECANIM_DEBUG
					//Debug.Log( "Load indexAssetPath:" + indexAssetPath );
					#endif
					TextAsset indexAsset = AssetDatabase.LoadAssetAtPath( indexAssetPath, typeof(TextAsset) ) as TextAsset;
					if( indexAsset != null ) {
						this.indexData = MMD4MecanimData.BuildIndexData( indexAsset );
						if( this.indexData != null ) {
							this.indexAsset = indexAsset;
						}
					}
				}
			}
		} else {
			this.indexData = null;
		}

		_CheckFBXMaterial();
	}

	private void _CheckModelInScene( List<Animator> assetAnimators, List<string> assetPaths, List<bool> assetIsSkinned )
	{
		if( this.isProcessing || !Setup() ) {
			return;
		}

		if( string.IsNullOrEmpty( this.fbxAssetPath ) ) {
			return;
		}
		
		for( int i = 0; i < assetPaths.Count; ++i ) {
			string assetPath = assetPaths[i];
			if( !string.IsNullOrEmpty( assetPath ) && assetPath == this.fbxAssetPath ) {
				if( assetIsSkinned[i] ) {
					string modelDataPath = GetModelDataPath( assetPath );
					string indexDataPath = GetIndexDataPath( assetPath );
					if( File.Exists( modelDataPath ) && File.Exists( indexDataPath ) ) {
						TextAsset modelData = AssetDatabase.LoadAssetAtPath( modelDataPath, typeof(TextAsset) ) as TextAsset;
						TextAsset indexData = AssetDatabase.LoadAssetAtPath( indexDataPath, typeof(TextAsset) ) as TextAsset;
						_MakeModel( assetAnimators[i].gameObject, modelData, indexData, assetIsSkinned[i] );
					}
				} else {
					string modelDataPath = GetModelDataPath( assetPath );
					if( File.Exists( modelDataPath ) ) {
						TextAsset modelData = AssetDatabase.LoadAssetAtPath( modelDataPath, typeof(TextAsset) ) as TextAsset;
						_MakeModel( assetAnimators[i].gameObject, modelData, null, assetIsSkinned[i] );
					}
				}
			}
		}
	}
	
	private void _MakeModel( GameObject modelGameObject, TextAsset modelData, TextAsset indexData, bool isSkinned )
	{
		if( modelData != null && (!isSkinned || indexData != null) ) {
			MMD4MecanimModel model = modelGameObject.AddComponent< MMD4MecanimModel >();
			model.modelFile = modelData;
			model.indexFile = indexData;

			// Add Animations.(Optional)
			if( this.pmx2fbxProperty.vmdAssetList != null ) {
				foreach( Object vmdAsset in this.pmx2fbxProperty.vmdAssetList ) {
					string vmdAssetPath = AssetDatabase.GetAssetPath( vmdAsset );
					if( !string.IsNullOrEmpty( vmdAssetPath ) ) {
						string animAssetPath = GetAnimDataPath( vmdAssetPath );
						TextAsset animAsset = AssetDatabase.LoadAssetAtPath( animAssetPath, typeof(TextAsset) ) as TextAsset;
						if( animAsset != null ) {
							if( model.animList == null ) {
								model.animList = new MMD4MecanimModel.Anim[1];
							} else {
								System.Array.Resize( ref model.animList, model.animList.Length + 1 );
							}
							
							MMD4MecanimModel.Anim anim = new MMD4MecanimModel.Anim();

							anim.animFile = animAsset;
							anim.animatorStateName = "Base Layer." + Path.GetFileNameWithoutExtension( animAsset.name ) + ".vmd";
							
							model.animList[model.animList.Length - 1] = anim;
						}
					}
				}
			}
		}
	}

	public bool Setup()
	{
		if( this.pmx2fbxProperty == null || this.pmx2fbxConfig == null ) {
			return _Setup();
		}

		return true;
	}

	public bool SetupWithReload()
	{
		return _Setup();
	}

	bool _Setup()
	{
		string scriptAssetPathWithoutExtension = _GetScriptAssetPathWithoutExtension();
		if( string.IsNullOrEmpty(scriptAssetPathWithoutExtension) ) {
			return false;
		}
		
		if( this.pmx2fbxProperty == null ) {
			this.pmx2fbxProperty = new PMX2FBXProperty();
		}
		
		/* Load config */
		if( this.pmx2fbxConfig == null ) {
			this.pmx2fbxConfig = GetPMX2FBXConfig( ( scriptAssetPathWithoutExtension + ".MMD4Mecanim.xml" ).Normalize(NormalizationForm.FormC) );
			if( this.pmx2fbxConfig == null ) { 
				this.pmx2fbxConfig = GetPMX2FBXConfig( GetPMX2FBXRootConfigPath() );
				if( this.pmx2fbxConfig == null ) {
					this.pmx2fbxConfig = new PMX2FBXConfig();
				} else {
					this.pmx2fbxConfig.renameList = null;
				}
			}

			/* Binding assets.( On loading only. ) */
			if( this.pmx2fbxConfig != null && this.pmx2fbxConfig.mmd4MecanimProperty != null ) {
				var mmd4MecanimProperty = this.pmx2fbxConfig.mmd4MecanimProperty;
				// Load PMX/PMD Asset ( Path )
				if( pmx2fbxProperty.pmxAsset == null ) {
					string pmxAssetPath = mmd4MecanimProperty.pmxAssetPath;
					if( System.IO.File.Exists( pmxAssetPath ) ) {
						pmx2fbxProperty.pmxAsset = AssetDatabase.LoadAssetAtPath( pmxAssetPath, typeof(Object) );
					}
				}
				// Load VMD
				this.pmx2fbxProperty.vmdAssetList = new List<Object>();
				if( mmd4MecanimProperty.vmdAssetPathList != null ) {
					for( int i = 0; i < mmd4MecanimProperty.vmdAssetPathList.Count; ++i ) {
						{
							string vmdAssetPath = mmd4MecanimProperty.vmdAssetPathList[i];
							Object vmdAsset = AssetDatabase.LoadAssetAtPath( vmdAssetPath, typeof(Object) );
							if( vmdAsset != null ) {
								pmx2fbxProperty.vmdAssetList.Add( vmdAsset );
							}
						}
					}
				}
				// Load FBX
				if( this.fbxAsset == null ) {
					{
						string fbxAssetPath = mmd4MecanimProperty.fbxAssetPath;
						if( !string.IsNullOrEmpty( fbxAssetPath ) && File.Exists( fbxAssetPath ) ) {
							this.fbxAsset = AssetDatabase.LoadAssetAtPath( fbxAssetPath, typeof(GameObject) ) as GameObject;
							this.fbxAssetPath = fbxAssetPath;
						}
					}
					if( this.fbxAsset == null ) {
						string fbxAssetPath = mmd4MecanimProperty.fbxOutputPath;
						if( !string.IsNullOrEmpty( fbxAssetPath ) && File.Exists( fbxAssetPath ) ) {
							this.fbxAsset = AssetDatabase.LoadAssetAtPath( fbxAssetPath, typeof(GameObject) ) as GameObject;
							this.fbxAssetPath = fbxAssetPath;
						}
					}
				}
			}
		}
		
		if( this.pmx2fbxConfig == null ) {
			this.pmx2fbxConfig = new PMX2FBXConfig();
		}
		if( this.pmx2fbxConfig.globalSettings == null ) {
			this.pmx2fbxConfig.globalSettings = new PMX2FBXConfig.GlobalSettings();
		}
		if( this.pmx2fbxConfig.bulletPhysics == null ) {
			this.pmx2fbxConfig.bulletPhysics = new PMX2FBXConfig.BulletPhysics();
		}
		if( this.pmx2fbxConfig.renameList == null ) {
			this.pmx2fbxConfig.renameList = new List<PMX2FBXConfig.Rename>();
		}
		if( this.pmx2fbxConfig.disableRigidBodyList == null ) {
			this.pmx2fbxConfig.disableRigidBodyList = new List<PMX2FBXConfig.DisableRigidBody>();
		}
		if( this.pmx2fbxConfig.freezeMotionList == null ) {
			this.pmx2fbxConfig.freezeMotionList = new List<PMX2FBXConfig.FreezeMotion>();
		}
		if( this.pmx2fbxConfig.mmd4MecanimProperty == null ) {
			this.pmx2fbxConfig.mmd4MecanimProperty = new PMX2FBXConfig.MMD4MecanimProperty();
		}
		if( string.IsNullOrEmpty( this.pmx2fbxConfig.mmd4MecanimProperty.fbxOutputPath ) ) {
			this.pmx2fbxConfig.mmd4MecanimProperty.fbxOutputPath = (scriptAssetPathWithoutExtension + ".fbx").Normalize(NormalizationForm.FormC);
		}

		if( this.pmx2fbxProperty.pmxAsset == null ) {
			string pmxAssetPath = scriptAssetPathWithoutExtension + ".pmx";
			if( File.Exists( pmxAssetPath ) ) {
				this.pmx2fbxProperty.pmxAsset = AssetDatabase.LoadAssetAtPath( pmxAssetPath, typeof(Object) );
			}
		}
		if( this.pmx2fbxProperty.pmxAsset == null ) {
			string pmxAssetPath = scriptAssetPathWithoutExtension + ".pmd";
			if( File.Exists( pmxAssetPath ) ) {
				this.pmx2fbxProperty.pmxAsset = AssetDatabase.LoadAssetAtPath( pmxAssetPath, typeof(Object) );
			}
		}
		if( this.fbxAsset == null ) {
			string fbxAssetPath = scriptAssetPathWithoutExtension + ".fbx";
			if( !string.IsNullOrEmpty( fbxAssetPath ) && File.Exists( fbxAssetPath ) ) {
				this.fbxAsset = AssetDatabase.LoadAssetAtPath( fbxAssetPath, typeof(GameObject) ) as GameObject;
				this.fbxAssetPath = fbxAssetPath;
			}
		}
		if( this.mmdModel == null ) {
			if( this.fbxAsset != null ) {
				string mmdModelPath = GetMMDModelPath( this.fbxAssetPath );
				if( !string.IsNullOrEmpty( mmdModelPath ) && File.Exists( mmdModelPath ) ) {
					this.mmdModel = GetMMDModel( mmdModelPath );
					this.mmdModelLastWriteTime = MMD4MecanimEditorCommon.GetLastWriteTime( mmdModelPath );
				}
			}
		}
		
		// Wine
		if( string.IsNullOrEmpty( this.pmx2fbxConfig.mmd4MecanimProperty.winePath ) ) {
			this.pmx2fbxConfig.mmd4MecanimProperty.wine = PMX2FBXConfig.Wine.Manual;
			this.pmx2fbxConfig.mmd4MecanimProperty.winePath = WinePaths[(int)PMX2FBXConfig.Wine.Manual];
			for( int i = 0; i < WinePaths.Length; ++i ) {
				if( File.Exists( WinePaths[i] ) ) {
					this.pmx2fbxConfig.mmd4MecanimProperty.wine = (PMX2FBXConfig.Wine)i;
					this.pmx2fbxConfig.mmd4MecanimProperty.winePath = WinePaths[i];
					break;
				}
			}
		}
		
		return true;
	}
	
	public void SavePMX2FBXConfig()
	{
		if( this.pmx2fbxConfig == null || this.pmx2fbxProperty == null ) {
			Debug.LogError("");
			return;
		}
		
		string scriptAssetPathWithoutExtension = _GetScriptAssetPathWithoutExtension();
		if( string.IsNullOrEmpty(scriptAssetPathWithoutExtension) ) {
			Debug.LogWarning( "Not found script." );
			return;
		}
		
		string pmx2fbxConfigPath = (scriptAssetPathWithoutExtension + ".MMD4Mecanim.xml").Normalize(NormalizationForm.FormC);
		
		if( this.pmx2fbxConfig.mmd4MecanimProperty != null ) {
			if( this.pmx2fbxProperty.pmxAsset != null ) {
				this.pmx2fbxConfig.mmd4MecanimProperty.pmxAssetPath = AssetDatabase.GetAssetPath( this.pmx2fbxProperty.pmxAsset );
			} else {
				this.pmx2fbxConfig.mmd4MecanimProperty.pmxAssetPath = null;
			}
			
			if( this.pmx2fbxProperty.vmdAssetList != null ) {
				this.pmx2fbxConfig.mmd4MecanimProperty.vmdAssetPathList = new List<string>();
				foreach( Object vmdAsset in this.pmx2fbxProperty.vmdAssetList ) {
					this.pmx2fbxConfig.mmd4MecanimProperty.vmdAssetPathList.Add( AssetDatabase.GetAssetPath( vmdAsset ) );
				}
			} else {
				this.pmx2fbxConfig.mmd4MecanimProperty.vmdAssetPathList = null;
			}
			
			if( this.fbxAsset != null ) {
				this.pmx2fbxConfig.mmd4MecanimProperty.fbxAssetPath = AssetDatabase.GetAssetPath( this.fbxAsset );
			} else {
				this.pmx2fbxConfig.mmd4MecanimProperty.fbxAssetPath = null;
			}
		}
		
		WritePMX2FBXConfig( pmx2fbxConfigPath, this.pmx2fbxConfig );
		AssetDatabase.ImportAsset( pmx2fbxConfigPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport );
	}
	
	private volatile System.Diagnostics.Process _pmx2fbxProcess;
	
	public bool isProcessing {
		get {
			return _pmx2fbxProcess != null;
		}
	}
	
	public void ProcessPMX2FBX()
	{
		if( this.pmx2fbxConfig == null ||
			this.pmx2fbxConfig.mmd4MecanimProperty == null ||
			this.pmx2fbxProperty == null ) {
			Debug.LogError("");
			return;
		}
		
		bool useWineFlag =  this.pmx2fbxConfig.mmd4MecanimProperty.useWineFlag;
		string pmx2fbxPath = GetPMX2FBXPath(useWineFlag );
		if( pmx2fbxPath == null ) {
			Debug.LogError("");
			return;
		}
		
		if( _pmx2fbxProcess != null ) {
			Debug.LogWarning( "Already processing pmx2fbx. Please wait." );
			return;
		}
		
		string scriptAssetPathWithoutExtension = _GetScriptAssetPathWithoutExtension();
		if( string.IsNullOrEmpty(scriptAssetPathWithoutExtension) ) {
			Debug.LogWarning( "Not found script." );
			return;
		}
		
		string pmx2fbxConfigPath = (scriptAssetPathWithoutExtension + ".MMD4Mecanim.xml").Normalize(NormalizationForm.FormC);
		
		string pmxAssetPath = this.pmx2fbxConfig.mmd4MecanimProperty.pmxAssetPath;
		if( string.IsNullOrEmpty( pmxAssetPath ) ) {
			Debug.LogError("PMX/PMD Path is null.");
			return;
		}
		
		string basePath = Application.dataPath;
		if( basePath.EndsWith( "Assets" ) ) {
			basePath = Path.GetDirectoryName( basePath );
		}

		System.Text.StringBuilder arguments = new System.Text.StringBuilder();
		
		if( Application.platform == RuntimePlatform.WindowsEditor || !useWineFlag ) {
			// Nothing.
		} else {
			arguments.Append( "\"" );
			arguments.Append( basePath + "/" + pmx2fbxPath );
			arguments.Append( "\" " );
		}
		
		arguments.Append( "-o \"" );
		arguments.Append( basePath + "/" + this.pmx2fbxConfig.mmd4MecanimProperty.fbxOutputPath );
		arguments.Append( "\" -conf \"" );
		arguments.Append( basePath + "/" + pmx2fbxConfigPath );
		arguments.Append( "\" \"" );
		arguments.Append( basePath + "/" + this.pmx2fbxConfig.mmd4MecanimProperty.pmxAssetPath );
		arguments.Append( "\"" );
		
		if( this.pmx2fbxConfig.mmd4MecanimProperty.vmdAssetPathList != null ) {
			foreach( string vmdAssetPath in this.pmx2fbxConfig.mmd4MecanimProperty.vmdAssetPathList ) {
				arguments.Append( " \"" );
				arguments.Append( basePath + "/" + vmdAssetPath );
				arguments.Append( "\"" );
			}
		}
		
		_pmx2fbxProcess = new System.Diagnostics.Process();
		
		if( Application.platform == RuntimePlatform.WindowsEditor || !useWineFlag ) {
			_pmx2fbxProcess.StartInfo.FileName = basePath + "/" + pmx2fbxPath;
		} else {
			string winePath = WinePaths[(int)this.pmx2fbxConfig.mmd4MecanimProperty.wine];
			if( this.pmx2fbxConfig.mmd4MecanimProperty.wine == PMX2FBXConfig.Wine.Manual ) {
				winePath = this.pmx2fbxConfig.mmd4MecanimProperty.winePath;
			}
			_pmx2fbxProcess.StartInfo.FileName = winePath;
		}
		_pmx2fbxProcess.StartInfo.Arguments = arguments.ToString();
        _pmx2fbxProcess.EnableRaisingEvents = true;
        _pmx2fbxProcess.Exited += _pmx2fbx_OnExited;
		
		if( !_pmx2fbxProcess.Start() ) {
			_pmx2fbxProcess.Dispose();
			_pmx2fbxProcess = null;
		}
	}
	
	void _pmx2fbx_OnExited(object sender, System.EventArgs e)
	{
		Debug.Log( "Processed pmx2fbx." );
		_pmx2fbxProcess.Dispose();
		_pmx2fbxProcess = null;

		_forceCheckChanged = true;
		MMD4MecanimImporterEditor.forceCheckChanged = true;
	}
}
