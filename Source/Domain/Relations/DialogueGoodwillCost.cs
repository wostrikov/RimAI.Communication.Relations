using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Module;
using UnityEngine;
using Verse;

namespace Ustas.RimAI.Communication.Relations.Relation
{
    public static class DialogueGoodwillCost
    {
        
        public const int BaseCost_RequestCaravan = -15;
        
        public const int BaseCost_RequestMilitaryAid = -25;
        
        public const int BaseCost_RequestMedicalAid = -25;
        
        public const int BaseCost_RequestResourceAid = -25;

        public const int BaseCost_CreateQuest = -10;
        
        public const int BaseCost_DemandLeave = -20;
        
        public const int BaseCost_DemandPayment = -15;
        
        public const int BaseGain_ShareIntel = 5;
        
        public const int BaseGain_SendGift = 8;
        
        public const int BaseGain_FulfillPromise = 10;
        
        public const int BaseGain_AcceptDemand = 5;
        
        public const int BaseGain_Apologize = 3;

        
        public const float TrustModifier = 0.05f;
        
        public const float IntimacyModifier = 0.03f;
        
        public const float ReciprocityModifier = 0.04f;
        
        public const float RespectModifier = 0.02f;
        
        public const float InfluenceModifier = 0.03f;

        
        public const int MaxSingleCost = -25;
        
        public const int MaxSingleGain = 15;
        
        public const int DailyCostLimit = -50;
        
        public const int DailyGainLimit = 30;

        
        public enum DialogueActionType
        {
            RequestCaravan,     
            RequestMilitaryAid, 
            RequestMedicalAid,  
            RequestResourceAid, 
            CreateQuest,        
            DemandLeave,        
            DemandPayment,      
            ShareIntel,         
            SendGift,           
            FulfillPromise,     
            AcceptDemand,       
            Apologize,          
            FriendlyChat,       
            Threaten,           
            Insult,             
            Compliment,         
            MakePromise,        
        }

        
        public static int GetBaseValue(DialogueActionType actionType)
        {
            int baseValue = actionType switch
            {
                DialogueActionType.RequestCaravan => BaseCost_RequestCaravan,
                DialogueActionType.RequestMilitaryAid => BaseCost_RequestMilitaryAid,
                DialogueActionType.RequestMedicalAid => BaseCost_RequestMedicalAid,
                DialogueActionType.RequestResourceAid => BaseCost_RequestResourceAid,
                DialogueActionType.CreateQuest => BaseCost_CreateQuest,
                DialogueActionType.DemandLeave => BaseCost_DemandLeave,
                DialogueActionType.DemandPayment => BaseCost_DemandPayment,
                DialogueActionType.ShareIntel => BaseGain_ShareIntel,
                DialogueActionType.SendGift => BaseGain_SendGift,
                DialogueActionType.FulfillPromise => BaseGain_FulfillPromise,
                DialogueActionType.AcceptDemand => BaseGain_AcceptDemand,
                DialogueActionType.Apologize => BaseGain_Apologize,
                DialogueActionType.FriendlyChat => 0,
                DialogueActionType.Threaten => -10,
                DialogueActionType.Insult => -8,
                DialogueActionType.Compliment => 3,
                DialogueActionType.MakePromise => 2,
                _ => 0
            };

            if (baseValue >= 0)
            {
                return baseValue;
            }

            float multiplier = GetDialogueActionCostMultiplier();
            return (int)Math.Floor(baseValue * multiplier);
        }

        private static float GetDialogueActionCostMultiplier()
        {
            float configured = RelationsMod.Instance?.InstanceSettings?.DialogueActionGoodwillCostMultiplier ?? 0.5f;
            return Mathf.Clamp(configured, 0f, 1f);
        }

        public static bool IsCostAction(DialogueActionType actionType)
        {
            int baseValue = GetBaseValue(actionType);
            return baseValue < 0;
        }

        public static bool IsRelationModified(DialogueActionType actionType)
        {
            return actionType switch
            {
                DialogueActionType.FriendlyChat => false,
                DialogueActionType.Insult => false,
                DialogueActionType.Compliment => false,
                _ => true
            };
        }

        public static int GetCooldownTicks(DialogueActionType actionType)
        {
            return actionType switch
            {
                DialogueActionType.RequestCaravan => 60000,     
                DialogueActionType.RequestMilitaryAid => 180000,
                DialogueActionType.RequestMedicalAid => 120000, 
                DialogueActionType.RequestResourceAid => 120000,
                DialogueActionType.CreateQuest => 0,           
                DialogueActionType.DemandLeave => 90000,        
                DialogueActionType.DemandPayment => 60000,      
                DialogueActionType.ShareIntel => 30000,         
                DialogueActionType.SendGift => 60000,           
                DialogueActionType.FulfillPromise => 0,         
                DialogueActionType.AcceptDemand => 0,           
                DialogueActionType.Apologize => 30000,          
                DialogueActionType.FriendlyChat => 0,           
                DialogueActionType.Threaten => 60000,           
                DialogueActionType.Insult => 30000,             
                DialogueActionType.Compliment => 0,             
                DialogueActionType.MakePromise => 0,            
                _ => 60000
            };
        }

        public static string GetActionLabel(DialogueActionType actionType)
        {
            return actionType switch
            {
                DialogueActionType.RequestCaravan => "请求商队",
                DialogueActionType.RequestMilitaryAid => "请求军事援助",
                DialogueActionType.RequestMedicalAid => "请求医疗援助",
                DialogueActionType.RequestResourceAid => "请求资源援助",
                DialogueActionType.CreateQuest => "创建任务",
                DialogueActionType.DemandLeave => "要求离开",
                DialogueActionType.DemandPayment => "要求支付",
                DialogueActionType.ShareIntel => "分享情报",
                DialogueActionType.SendGift => "赠送礼物",
                DialogueActionType.FulfillPromise => "履行承诺",
                DialogueActionType.AcceptDemand => "接受要求",
                DialogueActionType.Apologize => "道歉",
                DialogueActionType.FriendlyChat => "友好闲聊",
                DialogueActionType.Threaten => "威胁",
                DialogueActionType.Insult => "侮辱",
                DialogueActionType.Compliment => "赞美",
                DialogueActionType.MakePromise => "做出承诺",
                _ => actionType.ToString()
            };
        }

        /// <summary>
        /// Gets the localization key for a dialogue action label.
        /// </summary>
        public static string GetActionLabelKey(DialogueActionType actionType)
        {
            return actionType switch
            {
                DialogueActionType.RequestCaravan => "RimChat_DialogueActionLabel_RequestCaravan",
                DialogueActionType.RequestMilitaryAid => "RimChat_DialogueActionLabel_RequestMilitaryAid",
                DialogueActionType.RequestMedicalAid => "RimChat_DialogueActionLabel_RequestMedicalAid",
                DialogueActionType.RequestResourceAid => "RimChat_DialogueActionLabel_RequestResourceAid",
                DialogueActionType.CreateQuest => "RimChat_DialogueActionLabel_CreateQuest",
                DialogueActionType.DemandLeave => "RimChat_DialogueActionLabel_DemandLeave",
                DialogueActionType.DemandPayment => "RimChat_DialogueActionLabel_DemandPayment",
                DialogueActionType.ShareIntel => "RimChat_DialogueActionLabel_ShareIntel",
                DialogueActionType.SendGift => "RimChat_DialogueActionLabel_SendGift",
                DialogueActionType.FulfillPromise => "RimChat_DialogueActionLabel_FulfillPromise",
                DialogueActionType.AcceptDemand => "RimChat_DialogueActionLabel_AcceptDemand",
                DialogueActionType.Apologize => "RimChat_DialogueActionLabel_Apologize",
                DialogueActionType.FriendlyChat => "RimChat_DialogueActionLabel_FriendlyChat",
                DialogueActionType.Threaten => "RimChat_DialogueActionLabel_Threaten",
                DialogueActionType.Insult => "RimChat_DialogueActionLabel_Insult",
                DialogueActionType.Compliment => "RimChat_DialogueActionLabel_Compliment",
                DialogueActionType.MakePromise => "RimChat_DialogueActionLabel_MakePromise",
                _ => "RimChat_DialogueActionLabel_Unknown"
            };
        }

        public static string GetActionDescription(DialogueActionType actionType)
        {
            return actionType switch
            {
                DialogueActionType.RequestCaravan => "Попросити фракцію вислати караван до твоєї колонії",
                DialogueActionType.RequestMilitaryAid => "Попросити фракцію надіслати військову допомогу",
                DialogueActionType.RequestMedicalAid => "Попросити у фракції медичну допомогу",
                DialogueActionType.RequestResourceAid => "Попросити у фракції допомогу ресурсами",
                DialogueActionType.CreateQuest => "Запропонувати гравцеві завдання через рідний шаблон",
                DialogueActionType.DemandLeave => "Вимагати, щоб люди фракції залишили твою територію",
                DialogueActionType.DemandPayment => "Вимагати від фракції відшкодування або викупу",
                DialogueActionType.ShareIntel => "Поділитися з фракцією цінними відомостями",
                DialogueActionType.SendGift => "向派系赠送礼物",
                DialogueActionType.FulfillPromise => "履行之前做出的承诺",
                DialogueActionType.AcceptDemand => "接受派系提出的要求",
                DialogueActionType.Apologize => "为之前的冒犯道歉",
                DialogueActionType.FriendlyChat => "进行友好的闲聊",
                DialogueActionType.Threaten => "对派系进行威胁",
                DialogueActionType.Insult => "侮辱派系",
                DialogueActionType.Compliment => "赞美派系",
                DialogueActionType.MakePromise => "向派系做出承诺",
                _ => actionType.ToString()
            };
        }
    }

    public class DialogueActionRecord : IExposable
    {
        public DialogueGoodwillCost.DialogueActionType ActionType;
        public int GoodwillChange;
        public int Tick;
        public string FactionName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ActionType, "actionType");
            Scribe_Values.Look(ref GoodwillChange, "goodwillChange", 0);
            Scribe_Values.Look(ref Tick, "tick", 0);
            Scribe_Values.Look(ref FactionName, "factionName", "");
        }
    }
}
