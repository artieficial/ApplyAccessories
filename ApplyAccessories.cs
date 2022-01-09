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

[ExecuteInEditMode]
public class ApplyAccessories
{
    private Transform _armature;
    private VRCExpressionParameters _avatarParameters;

    private VRCAvatarDescriptor _avatar;
    private GameObject _accessory;
    private bool _nestArmature;

    private bool _reuseMenu;
    private VRCExpressionsMenu _menu;
    private Dictionary<string, VRCExpressionsMenu> _menus = new Dictionary<string, VRCExpressionsMenu>();

    private Dictionary<string, AnimationClip> _customAnims = new Dictionary<string, AnimationClip>();

    private AnimationClip _femaleAnim;
    private string _femaleBlendShape;

    private SkinnedMeshRenderer[] _associatedAccessories = {};
    private Dictionary<SkinnedMeshRenderer, string[]> _associatedAccessoryBlendShapes = new Dictionary<SkinnedMeshRenderer, string[]>();

    private HashSet<int> _visitedBones = new HashSet<int>();
    private Dictionary<Transform, DynamicBone> _dynamicBones = new Dictionary<Transform, DynamicBone>();

    public ApplyAccessories()
    {
        Undo.undoRedoPerformed += AssetDatabase.SaveAssets;
    }

    public void setAvatar(VRCAvatarDescriptor avatar)
    {
        _avatar = avatar;
    }

    public void setAccessory(GameObject accessory)
    {
        _accessory = accessory;
    }

    public void setNestArmature(bool nestArmature)
    {
        _nestArmature = nestArmature;
    }

    public void setReuseMenu(bool reuseMenu)
    {
        _reuseMenu = reuseMenu;
    }

    public void setMenu(VRCExpressionsMenu menu)
    {
        _menu = menu;
    }

    public void setMenus(Dictionary<string, VRCExpressionsMenu> menus)
    {
        _menus = menus;
    }

    public void setCustomAnims(Dictionary<string, AnimationClip> customAnims)
    {
        _customAnims = customAnims;
    }

    public void setFemaleAnim(AnimationClip femaleAnim)
    {
        _femaleAnim = femaleAnim;
    }

    public void setFemaleBlendShape(string femaleBlendShape)
    {
        _femaleBlendShape = femaleBlendShape;
    }

    public void setAssociatedAccessories(SkinnedMeshRenderer[] associatedAccessories)
    {
        _associatedAccessories = associatedAccessories;
    }

    public void setAssociatedAccessoryBlendShapes(Dictionary<SkinnedMeshRenderer, string[]> associatedAccessorBlendShapes)
    {
        _associatedAccessoryBlendShapes = associatedAccessorBlendShapes;
    }

    AnimatorController getFXController()
    {
        VRCAvatarDescriptor.CustomAnimLayer fx = Array.Find(_avatar.baseAnimationLayers,
            l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
        return fx.animatorController as AnimatorController;        
    }

    AnimationClip createAnimationClip(string name)
    {
        if(!AssetDatabase.IsValidFolder("Assets/GeneratedAnimations"))
        {    
            AssetDatabase.CreateFolder("Assets", "GeneratedAnimations");
        }
        AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/GeneratedAnimations/{name}.anim");
        AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f));
    
        if (!animationClip) {
            animationClip = new AnimationClip();
            animationClip.SetCurve(name, typeof(GameObject), "m_IsActive", animationCurve);
            AssetDatabase.CreateAsset(animationClip, $"Assets/GeneratedAnimations/{name}.anim");
        }
        else
        {
            animationClip.SetCurve(name, typeof(GameObject), "m_IsActive", animationCurve);
        }

        AnimationCurve blendShapeCurve = new AnimationCurve(new Keyframe(0.0f, 100.0f));
        foreach(SkinnedMeshRenderer accessory in _associatedAccessories)
        {
            if (accessory != null)
            {
                Undo.RegisterCompleteObjectUndo(accessory, "Accessory Animation");
                string[] blendShapes = _associatedAccessoryBlendShapes[accessory];
                foreach(string blendShape in blendShapes)
                {
                    animationClip.SetCurve(accessory.gameObject.name, typeof(SkinnedMeshRenderer), $"blendShape.{blendShape}", blendShapeCurve);
                }
            }
        }

        AssetDatabase.SaveAssets();
        
        return animationClip;
    }

    void updateAnimatorController(string name, AnimationClip animationClip)
    {
        AnimatorController animatorController = getFXController();
        AnimatorControllerLayer animatorControllerLayer;

        // If the parameter already exists, then we already created everything and this step is unnecessary.
        if (Array.Find(animatorController.parameters, (parameter) => { return parameter.name == name; } ) == null)
            animatorController.AddParameter(name, AnimatorControllerParameterType.Bool);
        else
            return;

        Undo.RegisterCompleteObjectUndo(animatorController, "Animator Controller");
        animatorControllerLayer = new AnimatorControllerLayer {
            defaultWeight = 1.0f,
            name = name,
            stateMachine = new AnimatorStateMachine()
        };

        AnimatorStateMachine stateMachine = animatorControllerLayer.stateMachine;
        AnimatorState wait = stateMachine.AddState("Wait");
        AnimatorState target = stateMachine.AddState(name);

        AnimatorCondition enableCondition = new AnimatorCondition {
            mode = AnimatorConditionMode.If,
            parameter = name
        };
        AnimatorCondition[] enableConditions = { enableCondition };
        AnimatorStateTransition enableTransition = new AnimatorStateTransition {
            conditions = enableConditions,
            destinationState = target,
            duration = 0.0f
        };
        wait.AddTransition(enableTransition);

        AnimatorCondition disableCondition = new AnimatorCondition {
            mode = AnimatorConditionMode.IfNot,
            parameter = name
        };
        AnimatorCondition[] disableConditions = { disableCondition };
        AnimatorStateTransition disableTransition = new AnimatorStateTransition {
            conditions = disableConditions,
            destinationState = wait,
            duration = 0.0f
        };
        target.AddTransition(disableTransition);
        target.motion = animationClip;

        string animatorControllerPath = AssetDatabase.GetAssetPath(animatorController);
        AssetDatabase.AddObjectToAsset(stateMachine, animatorControllerPath);
        AssetDatabase.AddObjectToAsset(wait, animatorControllerPath);
        AssetDatabase.AddObjectToAsset(target, animatorControllerPath);
        AssetDatabase.AddObjectToAsset(enableTransition, animatorControllerPath);
        AssetDatabase.AddObjectToAsset(disableTransition, animatorControllerPath);

        animatorController.AddLayer(animatorControllerLayer);
        AssetDatabase.SaveAssets();
    }

    bool hasEnoughParameters(string[] names, int required)
    {
        int usedBits = 0;
        foreach (VRCExpressionParameters.Parameter param in _avatar.expressionParameters.parameters)
        {
            if (param != null && param.name != "" && !Array.Exists(names, element => element == param.name))
            {
                usedBits += param.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            }
        }
        return usedBits <= (128 - required);
    }

    VRCExpressionParameters.Parameter addParameterToExpressions(string name, int required)
    {
        Undo.RegisterCompleteObjectUndo(_avatarParameters, "Target Avatar Parameters");
        VRCExpressionParameters.Parameter[] existingParameters = _avatarParameters.parameters;

        foreach (VRCExpressionParameters.Parameter parameter in existingParameters)
        {
            if (parameter.name == name)
            {
                return parameter;
            }
        }

        VRCExpressionParameters.Parameter p = new VRCExpressionParameters.Parameter {
            name = name,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f,
            saved = true
        };

        Array.Resize(ref existingParameters, existingParameters.Length + 1);
        existingParameters[existingParameters.Length - 1] = p;
        _avatarParameters.parameters = existingParameters;

        return p;
    }

    void addParameterToSubmenu(string name, int required, VRCExpressionsMenu menu)
    {
        Undo.RegisterCompleteObjectUndo(menu, "Target Submenu");
        foreach (VRCExpressionsMenu.Control control in menu.controls)
        {
            if (control.name == name)
            {
                return;
            }
        }

        VRCExpressionsMenu.Control targetControl = new VRCExpressionsMenu.Control {
            name = name,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter {
                name = name
            }
        };
        menu.controls.Add(targetControl);
    }

    void updateFemaleAnimationClip(string name)
    {
        Undo.RegisterCompleteObjectUndo(_femaleAnim, "Target female animation clip");
        AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0.0f, 100.0f));
        _femaleAnim.SetCurve(name, typeof(SkinnedMeshRenderer), $"blendShape.{_femaleBlendShape}", animationCurve);

        AssetDatabase.SaveAssets();
    }

    Transform[] recurseBones(int s, Transform[] sourceBones, List<Transform> targetBones, ref SkinnedMeshRenderer subAccessory)
    {
        if (_visitedBones.Contains(s))
        {
            return sourceBones;
        }
        _visitedBones.Add(s);

        for (int t = 0; t < targetBones.Count; t++)
        {
            if (sourceBones[s].name.Contains(targetBones[t].name) || sourceBones[s].name.Replace('_', ' ').Contains(targetBones[t].name))
            {
                Component[] components = sourceBones[s].gameObject.GetComponents<Component>();
                if (components.Length > 1 || _nestArmature)
                {
                    if (sourceBones[s].name == targetBones[t].name) {
                        sourceBones[s].name += $"_{subAccessory.name}";
                        if (subAccessory.rootBone == sourceBones[s])
                        {
                            subAccessory.rootBone = targetBones[t];
                        }
                        foreach (Transform child in sourceBones[s])
                        {
                            if (child.parent == sourceBones[s])
                            {
                                int sc = Array.FindIndex(sourceBones, x => x == child);

                                if (sc >= 0 && sc < sourceBones.Length)
                                    sourceBones = recurseBones(sc, sourceBones, targetBones, ref subAccessory);

                                if (sourceBones[sc].parent == sourceBones[s])
                                    Undo.SetTransformParent(child, targetBones[t], sourceBones[s].gameObject.name);
                            }
                        }
                        sourceBones[s] = targetBones[t];
                        break;
                    }
                    Undo.SetTransformParent(sourceBones[s], targetBones[t], sourceBones[s].gameObject.name);
                }
                else
                {
                    if (subAccessory.rootBone == sourceBones[s])
                    {
                        subAccessory.rootBone = targetBones[t];
                    }
                    foreach (Transform child in sourceBones[s])
                    {
                        if (child.parent == sourceBones[s])
                        {
                            int sc = Array.FindIndex(sourceBones, x => x == child);

                            if (sc >= 0 && sc < sourceBones.Length)
                                sourceBones = recurseBones(sc, sourceBones, targetBones, ref subAccessory);

                            if (sourceBones[sc].parent == sourceBones[s])
                                Undo.SetTransformParent(child, targetBones[t], sourceBones[s].gameObject.name);
                        }
                    }
                    sourceBones[s] = targetBones[t];
                }
            }
        }

        return sourceBones;
    }

    [ContextMenu("Reassign Bones")]
    public void Reassign()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Apply Accessories");
        int undoGroupIndex = Undo.GetCurrentGroup();

        if (_avatar == null) {
            Debug.LogError("You need to assign a target avatar descriptor");
            return;
        }

        // Create a duplicate that isn't a prefab and delete the prefab, so it can be return on undo
        GameObject accessory = UnityEngine.Object.Instantiate(_accessory);
        Undo.RegisterCreatedObjectUndo(accessory, "Accessory");
        Undo.DestroyObjectImmediate(_accessory);
        _accessory = accessory;

        SkinnedMeshRenderer[] accessoryMeshRenderers = _accessory.GetComponentsInChildren<SkinnedMeshRenderer>();

        _armature = _avatar.transform.Find("Armature");
        _avatarParameters = _avatar.expressionParameters;

        string[] names = {};
        Array.Resize(ref names, accessoryMeshRenderers.Length);
        for (int i = 0; i < accessoryMeshRenderers.Length; i++)
        {
            names[i] = accessoryMeshRenderers[i].name;
        }

        if ((_menu != null || _reuseMenu) && !hasEnoughParameters(names, accessoryMeshRenderers.Length))
        {
            Debug.LogError("Not enough available parameters to add all accessories. Remove the Submenu parameter to reassign armature and have accessories permanently on.");
            return;
        }

        for (int a = 0; a < accessoryMeshRenderers.Length; a++)
        {
            SkinnedMeshRenderer subAccessory = accessoryMeshRenderers[a];
            Undo.RecordObject(subAccessory.gameObject, "Accessory");

            Transform[] sourceBones = subAccessory.bones;
            List<Transform> targetBones = new List<Transform>(_armature.GetComponentsInChildren<Transform>());

            if (Type.GetType("DynamicBone") != null)
            {
                DynamicBone[] dynamicBones = subAccessory.gameObject.GetComponentsInChildren<DynamicBone>();

                for (int i = 0; i < dynamicBones.Length; i++)
                {
                    for (int t = 0; t < targetBones.Count; t++)
                    {
                        if (dynamicBones[i].m_Root.name.Contains(targetBones[t].name) || dynamicBones[i].m_Root.name.Replace('_', ' ').Contains(targetBones[t].name))
                        {
                            dynamicBones[i].m_Root = targetBones[t];
                        }
                    }

                    for (int j = 0; j < dynamicBones[i].m_Roots.Count; j++)
                    {
                        for (int t = 0; t < targetBones.Count; t++)
                        {
                            if (dynamicBones[i].m_Roots[j].name.Contains(targetBones[t].name) || dynamicBones[i].m_Roots[j].name.Replace('_', ' ').Contains(targetBones[t].name))
                            {
                                dynamicBones[i].m_Roots[j] = targetBones[t];
                            }
                        }
                    }
                }
            }

            for (int s = 0; s < sourceBones.Length; s++)
            {
                if (_visitedBones.Contains(s))
                    continue;

                sourceBones = recurseBones(s, sourceBones, targetBones, ref subAccessory);
            }

            _visitedBones.Clear();

            for (int t = 0; t < targetBones.Count; t++)
            {
                if (targetBones[t].parent == _armature && subAccessory.rootBone == null)
                {
                    subAccessory.rootBone = targetBones[t];
                }  
            }

            subAccessory.bones = sourceBones;
            
            VRCExpressionsMenu menu = null;
            if (_reuseMenu)
                menu = _menu;
            else if (_menus.ContainsKey(subAccessory.name))
                menu = _menus[subAccessory.name];

            if (menu != null)
            {
                addParameterToExpressions(subAccessory.name, accessoryMeshRenderers.Length);
                addParameterToSubmenu(subAccessory.name, accessoryMeshRenderers.Length, menu);
            }

            AnimationClip animationClip;
            if (_customAnims.ContainsKey(subAccessory.name))
                animationClip = _customAnims[subAccessory.name];
            else
                animationClip = createAnimationClip(subAccessory.name);
            updateAnimatorController(subAccessory.name, animationClip);

            if (_femaleAnim != null)
                updateFemaleAnimationClip(subAccessory.name);

            subAccessory.gameObject.SetActive(false);
            Undo.SetTransformParent(subAccessory.gameObject.transform, _avatar.gameObject.transform, subAccessory.name);
        }

        Undo.DestroyObjectImmediate(_accessory);
        Undo.CollapseUndoOperations(undoGroupIndex);
    }
}
