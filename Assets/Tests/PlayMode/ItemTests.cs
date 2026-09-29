using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

/// <summary>
/// 4단계 확인: 금화 배치, 회전·떠다니기, 획득, ITEM 개수 UI.
/// </summary>
public class ItemTests : PlayModeTestBase
{
    // 배치 B안: (1,1), (2,2), (3,3) 칸, 바닥에서 0.5 위
    static readonly Vector3[] ItemPositions =
    {
        new Vector3(-0.5f, 0.5f, -0.5f),
        new Vector3(0.5f, 0.5f, 0.5f),
        new Vector3(1.5f, 0.5f, 1.5f),
    };

    TMP_Text itemText;

    [UnitySetUp]
    public IEnumerator FindUI()
    {
        var textGo = GameObject.Find("ItemText");
        Assert.IsNotNull(textGo, "ItemText UI가 있어야 합니다.");
        itemText = textGo.GetComponent<TMP_Text>();
        yield break;
    }

    // 테스트 어셈블리는 게임 스크립트(Item)를 직접 참조할 수 없어서 오브젝트 이름으로 찾는다
    static Transform ItemsRoot => GameObject.Find("Level/Items")?.transform;
    static int ItemCount => ItemsRoot != null ? ItemsRoot.childCount : 0;

    Transform FindItemAt(Vector3 position)
    {
        foreach (Transform item in ItemsRoot)
            if (Vector3.Distance(item.position, position) < 0.05f) return item;
        return null;
    }

    [UnityTest]
    public IEnumerator ThreeItems_AtPlannedPositions_AndUIShowsZero()
    {
        Assert.AreEqual(3, ItemCount, "아이템은 3개여야 합니다.");
        foreach (var p in ItemPositions)
            Assert.IsNotNull(FindItemAt(p), $"{p} 위치에 아이템이 있어야 합니다.");
        Assert.AreEqual("ITEM 0 / 3", itemText.text);
        yield break;
    }

    [UnityTest]
    public IEnumerator Item_SpinsAndBobs()
    {
        var coin = FindItemAt(ItemPositions[0]).Find("Coin");
        var rot0 = coin.localRotation;
        float minY = 10f, maxY = -10f;
        for (var t = 0f; t < 2f; t += Time.deltaTime)
        {
            minY = Mathf.Min(minY, coin.localPosition.y);
            maxY = Mathf.Max(maxY, coin.localPosition.y);
            yield return null;
        }
        Assert.Greater(Quaternion.Angle(rot0, coin.localRotation), 1f, "금화가 회전해야 합니다.");
        Assert.Greater(maxY - minY, 0.1f, "금화가 위아래로 떠다녀야 합니다.");
    }

    [UnityTest]
    public IEnumerator WalkingIntoItem_CollectsIt_AndUpdatesUI()
    {
        var item = FindItemAt(ItemPositions[0]).gameObject;
        yield return Hold(0.5f, Key.W, Key.D); // 시작 위치에서 대각선으로 걸어 (1,1) 칸의 금화를 지나감

        Assert.AreEqual("ITEM 1 / 3", itemText.text, "금화를 먹으면 개수가 올라가야 합니다.");
        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(item == null, "먹은 금화는 연출 후 사라져야 합니다.");
    }

    [UnityTest]
    public IEnumerator CollectEffect_ShrinksAndRises()
    {
        var item = FindItemAt(ItemPositions[0]);
        var coin = item.Find("Coin");
        var y0 = coin.position.y;
        Teleport(new Vector3(-0.5f, 0f, -0.5f));
        yield return new WaitForSeconds(0.15f); // 연출 중간
        Assert.IsFalse(item.GetComponent<Collider>().enabled, "먹으면 다시 먹히지 않도록 콜라이더가 꺼져야 합니다.");
        Assert.Less(coin.localScale.x, 0.9f, "작아져야 합니다.");
        Assert.Greater(coin.position.y, y0 + 0.05f, "떠올라야 합니다.");
    }

    [UnityTest]
    public IEnumerator CollectingAll_ShowsThreeOfThree()
    {
        foreach (var p in ItemPositions)
        {
            Teleport(new Vector3(p.x, 0f, p.z));
            yield return new WaitForSeconds(0.2f);
        }
        Assert.AreEqual("ITEM 3 / 3", itemText.text);
        yield return new WaitForSeconds(0.4f);
        Assert.AreEqual(0, ItemCount, "모든 금화가 사라져야 합니다.");
    }
}
