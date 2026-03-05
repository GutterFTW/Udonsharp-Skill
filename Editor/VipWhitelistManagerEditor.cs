using UnityEngine;

#if !COMPILER_UDONSHARP && UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UdonSharpEditor;

[CustomEditor(typeof(VipWhitelistManager))]
public class VipWhitelistManagerEditor : Editor
{
    private SerializedProperty roleNamesProp;
    private SerializedProperty roleUrlsProp;
    private SerializedProperty roleColorsProp;
    private SerializedProperty roleCanAddProp;
    private SerializedProperty roleCanRevokeProp;
    private SerializedProperty roleCanVipProp;
    private SerializedProperty roleCanReadOnlyProp;
    private SerializedProperty roleCanDjProp;
    private SerializedProperty debugProp;
    private SerializedProperty logColorProp;

    private ReorderableList list;

    private void OnEnable()
    {
        roleNamesProp = serializedObject.FindProperty("roleNames");
        roleUrlsProp = serializedObject.FindProperty("rolePastebinUrls");
        roleColorsProp = serializedObject.FindProperty("roleColors");
        roleCanAddProp = serializedObject.FindProperty("roleCanAddPlayers");
        roleCanRevokeProp = serializedObject.FindProperty("roleCanRevokePlayers");
        roleCanVipProp = serializedObject.FindProperty("roleCanVipAccess");
        roleCanReadOnlyProp = serializedObject.FindProperty("roleCanReadOnly");
        roleCanDjProp = serializedObject.FindProperty("roleCanDjAccess");
        debugProp = serializedObject.FindProperty("enableDebugLogs");
        logColorProp = serializedObject.FindProperty("logColor");

        if (roleNamesProp == null)
        {
            // ensure inspector doesn't break if property names differ
            return;
        }

        // create a reorderable list driven by the roleNames array; we will keep the other arrays in sync
        list = new ReorderableList(serializedObject, roleNamesProp, true, true, true, true);

        list.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Roles (Name / Pastebin URL / Color / Permissions)");
        };

        list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
            // ensure arrays are synced before drawing
            SyncArraySizes();

            SerializedProperty nameProp = roleNamesProp.GetArrayElementAtIndex(index);
            SerializedProperty urlProp = roleUrlsProp.GetArrayElementAtIndex(index);
            SerializedProperty colorProp = roleColorsProp.GetArrayElementAtIndex(index);
            SerializedProperty canAddProp = roleCanAddProp.GetArrayElementAtIndex(index);
            SerializedProperty canRevokeProp = roleCanRevokeProp.GetArrayElementAtIndex(index);

            // box padding and sizing
            float padding = 6f;
            float innerX = rect.x + padding;
            float innerY = rect.y + padding / 2f;
            float innerW = rect.width - padding * 2f;

            // compute box height (matches elementHeight)
            // now three rows: Name, URL, Permissions
            float boxHeight = EditorGUIUtility.singleLineHeight * 3 + padding * 2f + 8f;

            // draw box background/border
            Rect boxRect = new Rect(innerX - 2f, innerY - 2f, innerW + 4f, boxHeight);
            GUI.Box(boxRect, GUIContent.none);

            // calculate square color picker size to span both rows
            float colorSize = boxHeight - 8f; // small vertical margins inside the box
            if (colorSize < EditorGUIUtility.singleLineHeight) colorSize = EditorGUIUtility.singleLineHeight;

            // cap color size to a reasonable max so it doesn't dominate on very large inspectors
            float maxColor = 150f;
            if (colorSize > maxColor) colorSize = maxColor;

            // compute color rect anchored to the right; left area (content) size derived from that
            float colorX = innerX + innerW - colorSize - 4f;
            // content area spans from innerX+6 to colorX-6
            float contentLeft = innerX + 6f;
            float contentRight = colorX - 6f;
            float contentWidth = contentRight - contentLeft;
            if (contentWidth < 80f)
            {
                // fallback: allow color to shrink if space too small
                contentWidth = Mathf.Max(innerW - 12f, 80f);
                colorX = innerX + innerW - (innerW - contentWidth) + 4f; // keep color at rightmost
            }

            // top row: Name field (with label)
            Rect topRowRect = new Rect(contentLeft, innerY + 4f, Mathf.Max(contentWidth - 6f, 80f), EditorGUIUtility.singleLineHeight);
            float colorY = innerY + 4f; // align color with top row
            Rect colorRect = new Rect(colorX, colorY, colorSize, colorSize);

            // draw name and color
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 60f;
            EditorGUI.PropertyField(topRowRect, nameProp, new GUIContent("Name"));
            EditorGUIUtility.labelWidth = prevLabelWidth;
            EditorGUI.PropertyField(colorRect, colorProp, GUIContent.none);

            // permissions origin X is contentLeft

            // second row: Pastebin URL label + field (ensure it does not extend under the color square)
            Rect urlRowRect = new Rect(contentLeft, innerY + EditorGUIUtility.singleLineHeight + 10f, Mathf.Max(contentWidth - 6f, 80f), EditorGUIUtility.singleLineHeight);
            float prevLabelWidth2 = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 80f;
            EditorGUI.PropertyField(urlRowRect, urlProp, new GUIContent("Pastebin URL"));
            EditorGUIUtility.labelWidth = prevLabelWidth2;

            // draw permission toggles explicitly as checkboxes
            bool addVal = canAddProp.boolValue;
            bool revokeVal = canRevokeProp.boolValue;
            bool readOnlyVal = roleCanReadOnlyProp != null ? roleCanReadOnlyProp.GetArrayElementAtIndex(index).boolValue : false;
            bool vipVal = roleCanVipProp != null ? roleCanVipProp.GetArrayElementAtIndex(index).boolValue : false;

            // place permission checkboxes slightly below the URL row (small padding)
            float permsY = urlRowRect.y + EditorGUIUtility.singleLineHeight + 6f;

            // align permissions with content area (not the color/reserved area)
            float permsStartX = innerX + 6f;
            // compute four equal checkbox widths that fit within the contentWidth (leave small spacing)
            float permSpacing = 6f;
            // now five checkboxes: Add, Revoke, Read Only, VIP, DJ
            float permCheckboxWidth = (contentWidth - permSpacing * 4f) / 5f;
            Rect addRect = new Rect(permsStartX, permsY, permCheckboxWidth, EditorGUIUtility.singleLineHeight);
            Rect revokeRect = new Rect(permsStartX + (permCheckboxWidth + permSpacing) * 1f, permsY, permCheckboxWidth, EditorGUIUtility.singleLineHeight);
            Rect readOnlyRect = new Rect(permsStartX + (permCheckboxWidth + permSpacing) * 2f, permsY, permCheckboxWidth, EditorGUIUtility.singleLineHeight);
            Rect vipRect = new Rect(permsStartX + (permCheckboxWidth + permSpacing) * 3f, permsY, permCheckboxWidth, EditorGUIUtility.singleLineHeight);
            Rect djRect = new Rect(permsStartX + (permCheckboxWidth + permSpacing) * 4f, permsY, permCheckboxWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.BeginChangeCheck();
            addVal = EditorGUI.ToggleLeft(addRect, "Add", addVal);
            if (EditorGUI.EndChangeCheck()) canAddProp.boolValue = addVal;

            EditorGUI.BeginChangeCheck();
            revokeVal = EditorGUI.ToggleLeft(revokeRect, "Revoke", revokeVal);
            if (EditorGUI.EndChangeCheck()) canRevokeProp.boolValue = revokeVal;

            if (roleCanReadOnlyProp != null)
            {
                EditorGUI.BeginChangeCheck();
                readOnlyVal = EditorGUI.ToggleLeft(readOnlyRect, "Read Only", readOnlyVal);
                if (EditorGUI.EndChangeCheck()) roleCanReadOnlyProp.GetArrayElementAtIndex(index).boolValue = readOnlyVal;
            }

            if (roleCanVipProp != null)
            {
                EditorGUI.BeginChangeCheck();
                vipVal = EditorGUI.ToggleLeft(vipRect, "VIP", vipVal);
                if (EditorGUI.EndChangeCheck()) roleCanVipProp.GetArrayElementAtIndex(index).boolValue = vipVal;
            }
            // DJ permission
            if (roleCanDjProp != null)
            {
                EditorGUI.BeginChangeCheck();
                bool curDj = roleCanDjProp.GetArrayElementAtIndex(index).boolValue;
                curDj = EditorGUI.ToggleLeft(djRect, "DJ", curDj);
                if (EditorGUI.EndChangeCheck()) roleCanDjProp.GetArrayElementAtIndex(index).boolValue = curDj;
            }
        };

        // element height: two lines + padding
        // increase element height slightly to accommodate permission row
        list.elementHeight = EditorGUIUtility.singleLineHeight * 3 + 36f;

        list.onAddCallback = (ReorderableList l) => {
            serializedObject.Update();
            int newIndex = roleNamesProp.arraySize;
            roleNamesProp.arraySize++;
            roleUrlsProp.arraySize = Mathf.Max(roleUrlsProp.arraySize, roleNamesProp.arraySize);
            roleColorsProp.arraySize = Mathf.Max(roleColorsProp.arraySize, roleNamesProp.arraySize);
            roleCanAddProp.arraySize = Mathf.Max(roleCanAddProp.arraySize, roleNamesProp.arraySize);
            roleCanRevokeProp.arraySize = Mathf.Max(roleCanRevokeProp.arraySize, roleNamesProp.arraySize);
            if (roleCanVipProp != null) roleCanVipProp.arraySize = Mathf.Max(roleCanVipProp.arraySize, roleNamesProp.arraySize);
            if (roleCanReadOnlyProp != null) roleCanReadOnlyProp.arraySize = Mathf.Max(roleCanReadOnlyProp.arraySize, roleNamesProp.arraySize);
            if (roleCanDjProp != null) roleCanDjProp.arraySize = Mathf.Max(roleCanDjProp.arraySize, roleNamesProp.arraySize);

            // set defaults for new elements
            var nameProp = roleNamesProp.GetArrayElementAtIndex(newIndex);
            nameProp.stringValue = "";
            var urlProp = roleUrlsProp.GetArrayElementAtIndex(newIndex);
            // leave url null/default
            var colorProp = roleColorsProp.GetArrayElementAtIndex(newIndex);
            colorProp.colorValue = Color.white;
            var addProp = roleCanAddProp.GetArrayElementAtIndex(newIndex);
            addProp.boolValue = true;
            var revokeProp = roleCanRevokeProp.GetArrayElementAtIndex(newIndex);
            revokeProp.boolValue = true;
            if (roleCanVipProp != null)
            {
                var vipProp = roleCanVipProp.GetArrayElementAtIndex(newIndex);
                vipProp.boolValue = true;
            }
            if (roleCanReadOnlyProp != null)
            {
                var roProp = roleCanReadOnlyProp.GetArrayElementAtIndex(newIndex);
                roProp.boolValue = false;
            }

            serializedObject.ApplyModifiedProperties();
            l.index = newIndex;
        };

        list.onRemoveCallback = (ReorderableList l) => {
            serializedObject.Update();
            int index = l.index;
            if (index < 0 || index >= roleNamesProp.arraySize) return;

            roleNamesProp.DeleteArrayElementAtIndex(index);
            if (index < roleUrlsProp.arraySize) roleUrlsProp.DeleteArrayElementAtIndex(index);
            if (index < roleColorsProp.arraySize) roleColorsProp.DeleteArrayElementAtIndex(index);
            if (index < roleCanAddProp.arraySize) roleCanAddProp.DeleteArrayElementAtIndex(index);
            if (index < roleCanRevokeProp.arraySize) roleCanRevokeProp.DeleteArrayElementAtIndex(index);
            if (roleCanVipProp != null && index < roleCanVipProp.arraySize) roleCanVipProp.DeleteArrayElementAtIndex(index);
            if (roleCanReadOnlyProp != null && index < roleCanReadOnlyProp.arraySize) roleCanReadOnlyProp.DeleteArrayElementAtIndex(index);
            if (roleCanDjProp != null && index < roleCanDjProp.arraySize) roleCanDjProp.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();
            l.index = Mathf.Clamp(index - 1, 0, roleNamesProp.arraySize - 1);
        };

        list.onReorderCallbackWithDetails = (ReorderableList l, int oldIndex, int newIndex) => {
            // move corresponding elements in other arrays
            SyncArraySizes();
            MoveArrayElement(roleUrlsProp, oldIndex, newIndex);
            MoveArrayElement(roleColorsProp, oldIndex, newIndex);
            MoveArrayElement(roleCanAddProp, oldIndex, newIndex);
            MoveArrayElement(roleCanRevokeProp, oldIndex, newIndex);
            if (roleCanVipProp != null) MoveArrayElement(roleCanVipProp, oldIndex, newIndex);
            if (roleCanReadOnlyProp != null) MoveArrayElement(roleCanReadOnlyProp, oldIndex, newIndex);
            if (roleCanDjProp != null) MoveArrayElement(roleCanDjProp, oldIndex, newIndex);
            serializedObject.ApplyModifiedProperties();
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // draw UdonSharp header and handle its early-return
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target))
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // draw default inspector for other fields, excluding role arrays and debug
        DrawPropertiesExcluding(serializedObject, "roleNames", "rolePastebinUrls", "roleColors", "roleCanAddPlayers", "roleCanRevokePlayers", "roleCanVipAccess", "roleCanReadOnly", "roleCanDjAccess", "enableDebugLogs", "logColor");

        if (roleNamesProp == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // ensure parallel arrays are the same size
        SyncArraySizes();

        // draw reorderable list
        list.DoLayoutList();

        GUILayout.Space(6);

        // Draw Debug section at the bottom
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        if (debugProp != null) EditorGUILayout.PropertyField(debugProp, new GUIContent("Enable Debug Logs"));
        if (logColorProp != null) EditorGUILayout.PropertyField(logColorProp, new GUIContent("Log Color"));

        serializedObject.ApplyModifiedProperties();
    }

    private void SyncArraySizes()
    {
        if (roleNamesProp == null) return;
        int size = roleNamesProp.arraySize;
        if (roleUrlsProp.arraySize != size) roleUrlsProp.arraySize = size;
        if (roleColorsProp.arraySize != size) roleColorsProp.arraySize = size;
        if (roleCanAddProp.arraySize != size) roleCanAddProp.arraySize = size;
        if (roleCanRevokeProp.arraySize != size) roleCanRevokeProp.arraySize = size;
        if (roleCanVipProp != null && roleCanVipProp.arraySize != size) roleCanVipProp.arraySize = size;
        if (roleCanReadOnlyProp != null && roleCanReadOnlyProp.arraySize != size) roleCanReadOnlyProp.arraySize = size;
        if (roleCanDjProp != null && roleCanDjProp.arraySize != size) roleCanDjProp.arraySize = size;
    }

    private void MoveArrayElement(SerializedProperty arrayProp, int src, int dst)
    {
        if (arrayProp == null) return;
        if (src == dst) return;
        if (src < 0 || src >= arrayProp.arraySize) return;
        if (dst < 0 || dst >= arrayProp.arraySize) return;

        arrayProp.MoveArrayElement(src, dst);
    }

}
#endif
