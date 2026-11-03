using MelonLoader;
using UnityEngine;
using Manager;
using Manager.MaiStudio;
using MAI2.Util;

namespace maimai_rpc
{
    internal class maiUser
    {
        public static string GetUserName(int playerSlot)
        {
            var UserDataManager = Singleton<UserDataManager>.Instance;
            var PlayerData = UserDataManager.GetUserData(playerSlot);

            return PlayerData.Detail.UserName;
        }

        public static uint GetUserRating(int playerSlot)
        {
            var UserDataManager = Singleton<UserDataManager>.Instance;
            var PlayerData = UserDataManager.GetUserData(playerSlot);

            return PlayerData.Detail.Rating;
        }

        public static uint GetUserCourse(int playerSlot)
        {
            var UserDataManager = Singleton<UserDataManager>.Instance;
            var PlayerData = UserDataManager.GetUserData(playerSlot);

            return PlayerData.Detail.CourseRank;
        }

        public static uint GetUserCourseRank(int playerSlot)
        {
            var UserDataManager = Singleton<UserDataManager>.Instance;
            var PlayerData = UserDataManager.GetUserData(playerSlot);

            return PlayerData.Detail.CourseRank;
        }

        public static uint GetUserGradeRank(int playerSlot)
        {
            var UserDataManager = Singleton<UserDataManager>.Instance;
            var PlayerData = UserDataManager.GetUserData(playerSlot);

            return PlayerData.Detail.GradeRank;
        }
    }
}