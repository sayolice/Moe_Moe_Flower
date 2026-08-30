#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FlowerData의 gardenShape 필드만 5×5 클릭형 격자로 바꿔 그리고, 나머지 필드는 전부 기본
/// 인스펙터와 똑같이(선언 순서·헤더 그대로) 그린다.
///
/// [PropertyDrawer가 아니라 CustomEditor를 쓰는 이유] Unity는 PropertyAttribute 기반
/// PropertyDrawer를 배열/리스트 필드에 붙이면 "리스트 전체"가 아니라 "리스트의 각 원소"마다
/// 따로 적용한다(공식 문서에 명시된 제약) — 그래서 List&lt;Vector2Int&gt; 하나를 통째로 격자로
/// 그리는 용도로는 PropertyDrawer가 애초에 쓸 수 없고, 필드 소유자 클래스 전체의 인스펙터를
/// 직접 그리는 CustomEditor가 정답이다. 켜진 칸이 곧 리스트 원소가 되고, 저장 시 좌상단 기준
/// (0,0)으로 정규화한다 — 격자 "어디"를 클릭했는지는 편집 편의일 뿐, 실제 모양 데이터는 항상
/// 원점 기준이어야 GardenManager가 origin+offset으로 배치할 때 항상 같은 결과를 낸다.
///
/// 런타임 데이터 구조(List&lt;Vector2Int&gt;)는 전혀 바꾸지 않는다 — Editor 폴더 전용이라 빌드에는
/// 포함되지 않는다.
/// </summary>
[CustomEditor(typeof(FlowerData))]
public class FlowerDataEditor : Editor
{
    private const string GardenShapePropertyName = "gardenShape";
    private const int GridSize = 5;
    private const float CellSize = 22f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == GardenShapePropertyName)
            {
                DrawPolyominoGrid(iterator);
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPolyominoGrid(SerializedProperty listProperty)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("정원 — 폴리오미노 모양 (회전 없음)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("칸을 클릭해 켜고 끄면 그 모양대로 저장됩니다.", EditorStyles.miniLabel);

        HashSet<Vector2Int> current = ReadCurrentCells(listProperty);
        bool changed = false;

        // (0,0)은 격자의 좌상단. x는 오른쪽으로, y는 아래로 증가 — 실제 게임 좌표계의 위/아래
        // 방향과는 무관하다(이건 순수 편집 도구일 뿐, GardenManager는 이 좌표를 그냥 offset으로만 쓴다).
        for (int row = 0; row < GridSize; row++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            for (int col = 0; col < GridSize; col++)
            {
                Vector2Int cell = new Vector2Int(col, row);
                bool wasOn = current.Contains(cell);
                bool isOn = GUILayout.Toggle(wasOn, GUIContent.none, "Button",
                    GUILayout.Width(CellSize), GUILayout.Height(CellSize));

                if (isOn != wasOn)
                {
                    changed = true;
                    if (isOn) current.Add(cell);
                    else current.Remove(cell);
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField($"칸 {current.Count}개 — 저장 시 좌상단 (0,0) 기준으로 자동 정렬됩니다.",
                                    EditorStyles.miniLabel);
        EditorGUILayout.Space();

        if (changed)
            WriteNormalizedCells(listProperty, current);
    }

    private static HashSet<Vector2Int> ReadCurrentCells(SerializedProperty listProperty)
    {
        var cells = new HashSet<Vector2Int>();
        for (int i = 0; i < listProperty.arraySize; i++)
            cells.Add(listProperty.GetArrayElementAtIndex(i).vector2IntValue);
        return cells;
    }

    private static void WriteNormalizedCells(SerializedProperty listProperty, HashSet<Vector2Int> cells)
    {
        List<Vector2Int> normalized = Normalize(cells);

        listProperty.arraySize = normalized.Count;
        for (int i = 0; i < normalized.Count; i++)
            listProperty.GetArrayElementAtIndex(i).vector2IntValue = normalized[i];
    }

    /// <summary> 최소 x, 최소 y가 0이 되도록 전체를 평행이동한다. 비어 있으면 빈 리스트 그대로. </summary>
    private static List<Vector2Int> Normalize(HashSet<Vector2Int> cells)
    {
        if (cells.Count == 0) return new List<Vector2Int>();

        int minX = cells.Min(c => c.x);
        int minY = cells.Min(c => c.y);

        return cells
            .Select(c => new Vector2Int(c.x - minX, c.y - minY))
            .OrderBy(c => c.y).ThenBy(c => c.x) // 저장 순서를 보기 좋게 안정적으로 정렬
            .ToList();
    }
}
#endif
