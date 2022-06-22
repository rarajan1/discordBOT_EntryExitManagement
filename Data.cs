using System;
using System.Collections.Generic;
using System.Linq;


namespace discordBOT_EntryExitManagement
{

    class MemberDatas
    {            
        public int activeMemberCount = 0;
        public ulong discordServerID;
        private List<MemberData> members = new List<MemberData>();

        public MemberDatas(ulong discordServerID)
        {
            this.discordServerID = discordServerID;
        }

        public string UpdateMemberActive(string dataString)
        {
            string result = "";
            var datas = dataString.Split(',');

            for (int i = 0; i < datas.Length / 4; i++)
            {
                if(discordServerID == ulong.Parse(datas[i * 4]))
                {
                    //メンバーの名前から探して代入
                    var member = GetMemberData(datas[i * 4 + 1]);
                    if(member != null)
                    {
                        member.date = DateTime.Parse(datas[i * 4 + 2]);
                        member.userActive = bool.Parse(datas[i * 4 + 3]);
                    }

                    result += $"{datas[i * 4 + 2]},{datas[i * 4 + 1]},{datas[i * 4 + 3]}\n";
                }
            }
            activeMemberCount = members.Where(x => x.userActive == true).Count();
            return result;
        }

        public MemberData SetUserID(ulong ID)
        {
            if(members.Any(x => x.ID == ID))
            {
                var member = members.Where(x => x.ID == ID).ToList();
                return member[0];
            }
            else
            {
                var member = new MemberData(ID);
                members.Add(member);
                return member;
            }
        }
        public bool SetMemberName(ulong ID, string name)
        {
            if(null != GetMemberData(ID))
            {
                GetMemberData(ID).memberName = name;
                return true;
            }    
            else
                return false;
        }
        public bool SetAvatarURL(ulong ID, string URL)
        {
            if (null != GetMemberData(ID))
            {
                GetMemberData(ID).avatarURL = URL;
                return true;
            }
            else
                return false;
        }
        public bool SetActive(ulong ID, bool active)
        {
            if (null != GetMemberData(ID))
            {
                GetMemberData(ID).userActive = active;
                return true;
            }
            else
                return false;
        }
        public bool SetUpdateDate(ulong ID, DateTime UpdateDate)
        {
            if (null != GetMemberData(ID))
            {
                GetMemberData(ID).date = UpdateDate;
                return true;
            }
            else
                return false;
        }
        public MemberData GetMemberData(ulong ID)
        {
            if (members.Any(x => x.ID == ID))
            {
                var member = members.Where(x => x.ID == ID).ToList();
                return member[0];
            }
            else
                return null;
        }
        public MemberData GetMemberData(string Name)
        {
            if (members.Any(x => x.memberName == Name))
            {
                var member = members.Where(x => x.memberName == Name).ToList();
                return member[0];
            }
            else
                return null;
        }
    }

    class MemberData
    {
        public ulong ID;
        public string memberName;
        public string avatarURL;
        public bool userActive;
        public DateTime date;
        public MemberData(ulong ID)
        {
            this.ID = ID;
        }
        public override string ToString()
        {
            return $"{avatarURL},{memberName},{date},{userActive},";
        }
    }
}
