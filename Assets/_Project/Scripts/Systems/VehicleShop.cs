using System.Collections.Generic;
using UnityEngine;

// 상점 (M8) - 핸드폰 "상점" 탭에서 파는 음료 목록과 구매 로직
// 탈것 자체(소환/보관)는 VehicleMount가 담당하고, 여기서는 돈 차감 + 소모품 효과만 처리함
public static class VehicleShop
{
    public class DrinkItem
    {
        public string id;
        public string displayName;
        public int price;
        public float staminaRestorePercent; // 0~1 (스태미나 최대치 대비 회복 비율)
    }

    public static readonly List<DrinkItem> Drinks = new List<DrinkItem>
    {
        new DrinkItem { id = "water", displayName = "생수", price = 500, staminaRestorePercent = 0.3f },
        new DrinkItem { id = "energy", displayName = "에너지 드링크", price = 1500, staminaRestorePercent = 1f },
    };

    // 음료 구매 시도 - 돈이 부족하면 아무 효과 없이 false
    public static bool BuyDrink(DrinkItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (!DeliveryManager.SpendMoney(item.price))
        {
            Debug.Log("돈이 부족해요.");
            return false;
        }

        if (StaminaSystem.Instance != null)
        {
            StaminaSystem.Instance.Restore(StaminaSystem.Instance.maxStamina * item.staminaRestorePercent);
        }

        Debug.Log(item.displayName + " 구매! 스태미나를 회복했습니다.");
        return true;
    }
}
