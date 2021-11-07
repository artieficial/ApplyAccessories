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

[CustomEditor(typeof(ApplyAccessories))]
public class ApplyAccessoriesEditor : Editor
{
    public Transform targetArmature;
    public Transform targetRootBone;

    bool showOverrideArmature = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        ApplyAccessories applyAccessories = (ApplyAccessories)target;

        // Allow for setting the armature manually
        showOverrideArmature = EditorGUILayout.BeginFoldoutHeaderGroup(showOverrideArmature, "Override Default Armature");
        if (showOverrideArmature)
        {
            EditorGUILayout.ObjectField("Target Armature", targetArmature, typeof(Transform), true);
            EditorGUILayout.ObjectField("Target Root Bone", targetRootBone, typeof(Transform), true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // Apply actual changes
        if (GUILayout.Button("Apply Changes"))
        {
            applyAccessories.setTargetArmature(targetArmature);
            applyAccessories.setTargetRootBone(targetRootBone);
            applyAccessories.Reassign();
        }
    }
}

[ExecuteInEditMode]
public class ApplyAccessories : MonoBehaviour
{
    public VRCAvatarDescriptor targetAvatarDescriptor;
    public VRCExpressionsMenu targetSubMenu;

    private Transform targetArmature;
    private Transform targetRootBone;
    private VRCExpressionParameters targetAvatarParameters;

    public void setTargetArmature(Transform target)
    {
        targetArmature = target;
    }

    public void setTargetRootBone(Transform target)
    {
        targetRootBone = target;
    }

    public void setTargetSubMenu(VRCExpressionsMenu target)
    {
        targetSubMenu = target;
    }

    AnimatorController getFXController()
    {
        VRCAvatarDescriptor.CustomAnimLayer fx = Array.Find(targetAvatarDescriptor.baseAnimationLayers,
            l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
        return fx.animatorController as AnimatorController;        
    }

    AnimationClip createAnimationClip(string targetName)
    {
        if(!AssetDatabase.IsValidFolder("Assets/GeneratedAnimations"))
        {    
            AssetDatabase.CreateFolder("Assets", "GeneratedAnimations");
        }
        AnimationClip animationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/GeneratedAnimations/{targetName}.anim");
        AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f));
    
        if (!animationClip) {
            animationClip = new AnimationClip();
            animationClip.SetCurve(targetName, typeof(GameObject), "isActive", animationCurve);
            AssetDatabase.CreateAsset(animationClip, $"Assets/GeneratedAnimations/{targetName}.anim");
        }
        else
        {
            animationClip.SetCurve(targetName, typeof(GameObject), "isActive", animationCurve);
        }

        AssetDatabase.SaveAssets();
        
        return animationClip;
    }

    void updateAnimatorController(string targetName)
    {
        AnimatorController animatorController = getFXController();

        animatorController.AddParameter(targetName, AnimatorControllerParameterType.Bool);
        AnimatorControllerLayer layer = new AnimatorControllerLayer {
            defaultWeight = 1.0f,
            name = targetName,
            stateMachine = new AnimatorStateMachine()
        };
        animatorController.AddLayer(layer);

        AnimatorStateMachine stateMachine = layer.stateMachine;
        AnimatorState wait = stateMachine.AddState("Wait");
        AnimatorState target = stateMachine.AddState(targetName);

        AnimatorTransition defaultTransition = new AnimatorTransition {
            destinationState = wait,
        };
        AnimatorTransition[] entryTransitions = { defaultTransition };
        stateMachine.entryTransitions = entryTransitions;

        AnimatorCondition enableCondition = new AnimatorCondition {
            mode = AnimatorConditionMode.Equals,
            parameter = targetName,
            threshold = 1.0f
        };
        AnimatorCondition[] enableConditions = { enableCondition };
        AnimatorStateTransition enableTransition = new AnimatorStateTransition {
            conditions = enableConditions,
            destinationState = target,
            duration = 0.0f
        };
        wait.AddTransition(enableTransition);

        AnimatorCondition disableCondition = new AnimatorCondition {
            mode = AnimatorConditionMode.Equals,
            parameter = targetName,
            threshold = 0.0f
        };
        AnimatorCondition[] disableConditions = { disableCondition };
        AnimatorStateTransition disableTransition = new AnimatorStateTransition {
            conditions = disableConditions,
            destinationState = wait,
            duration = 0.0f
        };
        target.AddTransition(disableTransition);

        AnimationClip animationClip = createAnimationClip(targetName);
        target.motion = animationClip;
    }

    bool hasEnoughParameters(string[] targetNames, int required)
    {
        int usedBits = 0;
        foreach (VRCExpressionParameters.Parameter param in targetAvatarDescriptor.expressionParameters.parameters)
        {
            if (param != null && param.name != "" && !Array.Exists(targetNames, element => element == param.name))
            {
                usedBits += param.valueType == VRCExpressionParameters.ValueType.Bool ? 1 : 8;
            }
        }
        return usedBits <= (128 - required);
    }

    bool hasEnoughMenuControls(string[] targetNames, int required)
    {
        foreach (VRCExpressionsMenu.Control control in targetSubMenu.controls)
        {
            if (Array.Exists(targetNames, element => element == control.name))
            {
                required -= 1;
            }
        }
        return (8 - targetSubMenu.controls.Count) >= required;
    }

    VRCExpressionParameters.Parameter addParameterToExpressions(string targetName, int required)
    {
        VRCExpressionParameters.Parameter[] existingParameters = targetAvatarParameters.parameters;

        foreach (VRCExpressionParameters.Parameter parameter in existingParameters)
        {
            if (parameter.name == targetName)
            {
                return parameter;
            }
        }

        string[] targetNames = { targetName };

        VRCExpressionParameters.Parameter p = new VRCExpressionParameters.Parameter {
            name = targetName,
            valueType = VRCExpressionParameters.ValueType.Bool,
            defaultValue = 0.0f,
            saved = true
        };

        Array.Resize(ref existingParameters, existingParameters.Length + 1);
        existingParameters[existingParameters.Length - 1] = p;
        targetAvatarParameters.parameters = existingParameters;

        return p;
    }

    void addParameterToSubmenu(string targetName, int required)
    {
        foreach (VRCExpressionsMenu.Control control in targetSubMenu.controls)
        {
            if (control.name == targetName)
            {
                return;
            }
        }

        VRCExpressionsMenu.Control targetControl = new VRCExpressionsMenu.Control {
            name = targetName,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter {
                name = targetName
            }
        };
        targetSubMenu.controls.Add(targetControl);
    }

    [ContextMenu("Reassign Bones")]
    public void Reassign()
    {
        if (targetAvatarDescriptor == null) {
            Debug.Log("You need to assign a target avatar descriptor");
            return;
        }

        if (targetArmature == null)
        {
            targetArmature = targetAvatarDescriptor.transform.Find("Armature");
            if (targetArmature == null)
            {
                Debug.Log("Please manually specify the avatar armature");
                return;
            }
        }

        if (targetRootBone == null) {
            Transform[] children = targetArmature.GetComponentsInChildren<Transform>();
 
            bool found = false;
            foreach (Transform child in children)
            {
                if (child.parent == targetArmature)
                {
                    if (found)
                    {
                        Debug.Log("Please manually specify the avatar root bone");
                        return;
                    }
                    targetRootBone = child;
                    found = true;
                }
            }
            if (!found)
            {
                Debug.Log("Please manually specify the avatar root bone");
                return;
            }
        }

        try
        {
            PrefabUtility.UnpackPrefabInstance(this.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        catch (ArgumentException)
        {
            Debug.Log("Gameobject is already unpacked.");
        }

        targetAvatarParameters = targetAvatarDescriptor.expressionParameters;

        Debug.Log("Starting to reassign bones");
        SkinnedMeshRenderer[] meshRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
        string[] targetNames = {};
        Array.Resize(ref targetNames, meshRenderers.Length);

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            targetNames[i] = meshRenderers[i].name;
        }

        Debug.Log($"Processing {meshRenderers.Length} meshes");

        if (targetSubMenu != null && !hasEnoughParameters(targetNames, meshRenderers.Length))
        {
            Debug.LogError("Not enough available parameters to add all accessories. Remove the Submenu parameter to reassign armature and have accessories permanently on.");
            return;
        }

        if (targetSubMenu != null && !hasEnoughMenuControls(targetNames, meshRenderers.Length))
        {
            Debug.LogError("The selected submenu does not have enough control spots available to add all necessary controllers.");
            return;
        }

        foreach (SkinnedMeshRenderer meshRenderer in meshRenderers)
        {
            Debug.LogFormat("Starting to reassign bones for {0}", meshRenderer.gameObject.name);

            Transform[] sourceBones = meshRenderer.bones;
            Transform[] targetBones = targetArmature.GetComponentsInChildren<Transform>();

            for (int s = 0; s < sourceBones.Length; s++)
            {
                for (int t = 0; t < targetBones.Length; t++)
                {
                    if (sourceBones[s].name.Contains(targetBones[t].name)) {
                        sourceBones[s] = targetBones[t];
                        break;
                    }
                }
            }

            meshRenderer.rootBone = targetRootBone;
            meshRenderer.bones = sourceBones;

            if (targetSubMenu != null)
            {
                addParameterToExpressions(meshRenderer.name, meshRenderers.Length);
                addParameterToSubmenu(meshRenderer.name, meshRenderers.Length);
                updateAnimatorController(meshRenderer.name);
            }

            meshRenderer.gameObject.transform.parent = targetAvatarDescriptor.gameObject.transform;
        }

        DestroyImmediate(this.gameObject);
    }
}