using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif
using UnityEngine;
using UnityEngine.Animations;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

public class ApplyAccessoriesWindow : EditorWindow
{
    private String version = "v1.1.8 Standard";
    private ApplyAccessories applyAccessories = new ApplyAccessories();
    private Vector2 scroll;

    private GameObject _accessory;
    private VRCAvatarDescriptor _avatar;
    private bool _nestArmature;
    SkinnedMeshRenderer[] _accessoryMeshRenderers = {};

    bool _reuseMenu = true;
    private VRCExpressionsMenu _menu;
    private Dictionary<string, VRCExpressionsMenu> _menus = new Dictionary<string, VRCExpressionsMenu>();

    private bool _customAnimationClips = false;
    private Dictionary<string, AnimationClip> _customAnims = new Dictionary<string, AnimationClip>();

    private AnimationClip _femaleAnim;
    private string _femaleBlendShape = "";
    private GenericMenu _femaleDropdown = new GenericMenu();

    private SkinnedMeshRenderer[] _associatedAccessories = {};
    private Dictionary<SkinnedMeshRenderer, string[]> _associatedAccessoryBlendShapes = new Dictionary<SkinnedMeshRenderer, string[]>();
    
    bool showMenuOptions = false;
    bool showFemaleAnimationOptions = false;
    bool showCustomAnimationOptions = false;
    bool showAssociatedAccessoryOptions = false;
    bool[] showAssociatedAccesoryPanels = {};

    [MenuItem ("Window/Apply Accessories")]
    public static void ShowWindow()
    {
        //Show existing window instance. If one doesn't exist, make one.
        EditorWindow.GetWindow(typeof(ApplyAccessoriesWindow));
    }

    // Gives a description regarding what the plugin does
    private void Header()
    {
        GUIStyle styleTitle = new GUIStyle(GUI.skin.label);
        styleTitle.fontSize = 16;
        styleTitle.margin = new RectOffset(20, 20, 20, 20);
        EditorGUILayout.LabelField("Apply Accessories", styleTitle);
        EditorGUILayout.Space();

        GUIStyle styleVersion = new GUIStyle(GUI.skin.label);
        EditorGUILayout.LabelField(version, styleVersion);
        EditorGUILayout.Space();

        GUIStyle styleDescription = new GUIStyle(GUI.skin.label);
        styleDescription.wordWrap = true;
        EditorGUILayout.LabelField("The following script works like the old one, drag the game object you would have attached the component to into the \"Accessory\" slot. If the clothing/accessory clips or looks warped, try enabling the \"Don't Reuse Bones\" option. If something isn't working, try checking the Github page for an updated release, or add an issue.", styleDescription);
        EditorGUILayout.Space();
    }

    // Sets up all the baseline information
    private void MainOptions()
    {
        _avatar = EditorGUILayout.ObjectField("Avatar", _avatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

        // On setting an accessory, sets blendhsape information.
        GameObject accessory = EditorGUILayout.ObjectField("Accessory", _accessory, typeof(GameObject), true) as GameObject;
        if (accessory != _accessory)
        {
            _accessory = accessory;
            _accessoryMeshRenderers = _accessory.GetComponentsInChildren<SkinnedMeshRenderer>();

            HashSet<string> blendShapes = new HashSet<String>();

            foreach (SkinnedMeshRenderer meshRenderer in _accessoryMeshRenderers)
            {
                Mesh mesh = meshRenderer.sharedMesh;

                for (int i = 0; i < mesh.blendShapeCount; i++)
                    blendShapes.Add(mesh.GetBlendShapeName(i));
            }

            _femaleDropdown = new GenericMenu();
            foreach (string blendShape in blendShapes)
                _femaleDropdown.AddItem(new GUIContent(blendShape), _femaleBlendShape == blendShape, selectedBlendShape => _femaleBlendShape = (string)selectedBlendShape, blendShape);
        }
        _nestArmature = EditorGUILayout.Toggle("Don't Reuse Bones", _nestArmature);
    }

    // Sets up all information regarding where to place things within VRC menus
    private void MenuOptions()
    {
        showMenuOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showMenuOptions, "Menu Options");
        if (showMenuOptions && _accessory != null)
        {
            if (_accessoryMeshRenderers.Length <= 8)
                _reuseMenu = EditorGUILayout.Toggle("Reuse Menu", _reuseMenu);
            else
                _reuseMenu = false;

            if (_reuseMenu)
            {
                // All toggles end in the same menu
                VRCExpressionsMenu menu = EditorGUILayout.ObjectField("Menu", _menu, typeof(VRCExpressionsMenu), true) as VRCExpressionsMenu;
                if (menu != null)
                {
                    if (_accessoryMeshRenderers.Length + menu.controls.Count > 8)
                        Debug.LogError($"The selected menu does not have enough control spots left. The selected menu should have no more than {8 - _accessoryMeshRenderers.Length} existing controls.");
                    else
                        _menu = menu;
                }
            }
            else
            {
                // Toggles can be sent to different menus
                foreach (SkinnedMeshRenderer meshRenderer in _accessoryMeshRenderers)
                {
                    VRCExpressionsMenu menu = null;
                    if (_menus.ContainsKey(meshRenderer.name))
                        menu = _menus[meshRenderer.name];

                    menu = EditorGUILayout.ObjectField($"Menu for {meshRenderer.gameObject.name}", menu, typeof(VRCExpressionsMenu), true) as VRCExpressionsMenu;
                    if (menu != null)
                    {
                        if (menu.controls.Count > 7)
                            Debug.LogError($"The selected menu for {meshRenderer.gameObject.name} does not have enough control spots left. The selected menu should have no more than {8 - _accessoryMeshRenderers.Length} existing controls.");
                        else
                            _menus[meshRenderer.name] = menu;
                    }
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // Sets up all information regarding included animation files
    private void CustomAnimationOptions()
    {
        showCustomAnimationOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showCustomAnimationOptions, "Custom Animations");
        if (showCustomAnimationOptions && _accessory != null)
        {
            _customAnimationClips = EditorGUILayout.Toggle("Use Custom Animations", _customAnimationClips);

            if (_customAnimationClips)
            {
                SkinnedMeshRenderer[] meshRenderers = _accessory.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (SkinnedMeshRenderer meshRenderer in _accessoryMeshRenderers)
                {
                    AnimationClip animationClip = null;
                    if (_customAnims.ContainsKey(meshRenderer.name))
                        animationClip = _customAnims[meshRenderer.name];

                    animationClip = EditorGUILayout.ObjectField($"Animation for {meshRenderer.gameObject.name}", animationClip, typeof(AnimationClip), true) as AnimationClip;
                    if (animationClip != null)
                        _customAnims[meshRenderer.name] = animationClip;
                    else
                        _customAnims.Remove(meshRenderer.name);
                }
            } 
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // Sets up all information regarding any related female blendshapes
    private void FemaleAnimationOptions()
    {
        showFemaleAnimationOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showFemaleAnimationOptions, "Female Blendshape");
        if (showFemaleAnimationOptions && _accessory != null)
        {
            _femaleAnim = EditorGUILayout.ObjectField("Animation Clip", _femaleAnim, typeof(AnimationClip), true) as AnimationClip;

            if (EditorGUILayout.DropdownButton(new GUIContent(_femaleBlendShape), FocusType.Keyboard))
            {
                _femaleDropdown.ShowAsContext();
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // Sets up information regarding the blend shapes of any associated accessories
    private void AssociatedAccessoryOptions()
    {
        // Sets up tracking for all the associated mesh renderers
        showAssociatedAccessoryOptions = EditorGUILayout.BeginFoldoutHeaderGroup(showAssociatedAccessoryOptions, "Interactions with other Accessories");
        if (_accessory != null && showAssociatedAccessoryOptions)
        {
            int accessoryCount = EditorGUILayout.IntField("Number of Accessories", _associatedAccessories.Length);
            if (accessoryCount != _associatedAccessories.Length)
            {
                Array.Resize(ref _associatedAccessories, accessoryCount);
            }
            
            for (int i = 0; i < accessoryCount; i++)
            {
                _associatedAccessories[i] = EditorGUILayout.ObjectField($"Accessory #{i + 1}", _associatedAccessories[i], typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
                if (_associatedAccessories[i] != null && !_associatedAccessoryBlendShapes.ContainsKey(_associatedAccessories[i]))
                {
                    string[] blendShapes = {};
                    _associatedAccessoryBlendShapes[_associatedAccessories[i]] = blendShapes;
                }
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // If the accessory has been selected and there are associated accessories, show relate blendshapes.
        if (_accessory != null)
        {
            if (_associatedAccessories.Length != showAssociatedAccesoryPanels.Length)
            {
                Array.Resize(ref showAssociatedAccesoryPanels, _associatedAccessories.Length);
            }

            for (int i = 0; i < _associatedAccessories.Length; i++)
            {
                if (_associatedAccessories[i] != null && _associatedAccessoryBlendShapes.ContainsKey(_associatedAccessories[i]))
                {
                    showAssociatedAccesoryPanels[i] = EditorGUILayout.BeginFoldoutHeaderGroup(showAssociatedAccesoryPanels[i], $"Blendshapes for {_associatedAccessories[i].gameObject.name}");
                    string[] blendShapes = _associatedAccessoryBlendShapes[_associatedAccessories[i]];
                    int blendShapesCount = EditorGUILayout.IntField("Number of Blendshapes", blendShapes.Length);
                    Array.Resize(ref blendShapes, blendShapesCount);

                    for (int j = 0; j < blendShapesCount; j++)
                    {
                        Mesh mesh = _associatedAccessories[i].sharedMesh;
                        GenericMenu dropdown = new GenericMenu();
                        for (int k = 0; k < mesh.blendShapeCount; k++)
                        {
                            string blendShapeName = mesh.GetBlendShapeName(k);
                            int associatedAccessoryIndex = i;
                            int blendShapeIndex = j;
                            dropdown.AddItem(new GUIContent(blendShapeName), blendShapes[j] == blendShapeName, selected => {
                                _associatedAccessoryBlendShapes[_associatedAccessories[associatedAccessoryIndex]][blendShapeIndex] = (string)selected;
                            }, blendShapeName);
                        }

                        if (EditorGUILayout.DropdownButton(new GUIContent(blendShapes[j]), FocusType.Keyboard))
                        {
                            dropdown.ShowAsContext();
                        }
                    }

                    _associatedAccessoryBlendShapes[_associatedAccessories[i]] = blendShapes;
                    EditorGUILayout.EndFoldoutHeaderGroup();
                }
            }
        }
    }

    // Apply all the options back to the main ApplyAccessories class
    private void ApplyOptions()
    {
        applyAccessories.setAvatar(_avatar);
        applyAccessories.setAccessory(_accessory);
        applyAccessories.setNestArmature(_nestArmature);

        applyAccessories.setReuseMenu(_reuseMenu);
        applyAccessories.setMenu(_menu);
        applyAccessories.setMenus(_menus);

        applyAccessories.setCustomAnims(_customAnims);

        applyAccessories.setFemaleAnim(_femaleAnim);
        applyAccessories.setFemaleBlendShape(_femaleBlendShape);

        applyAccessories.setAssociatedAccessories(_associatedAccessories);
        applyAccessories.setAssociatedAccessoryBlendShapes(_associatedAccessoryBlendShapes);
    }

    void OnGUI()
    {
        Header();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        MainOptions();
        MenuOptions();
        CustomAnimationOptions();
        FemaleAnimationOptions();
        AssociatedAccessoryOptions();
        ApplyOptions();

        if (GUILayout.Button("Apply Changes"))
        {
            applyAccessories.Reassign();
        }

        EditorGUILayout.EndScrollView();
    }
}
