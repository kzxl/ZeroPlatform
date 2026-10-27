using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Tests
{
    public class UserPersonaAndPersonalizationTests
    {
        [Fact]
        public void UserPersona_DetectsRespectfulPronoun_AnhEm()
        {
            var persona = new UserPersona();
            persona.RecordUtterance("Anh ơi kiểm tra giúp anh đơn hàng SO-1082 với");

            Assert.Equal("anh", persona.UserPronoun);
            Assert.Equal("em", persona.BotPronoun);
            Assert.Equal(CommunicationTone.Respectful, persona.Tone);

            string formatted = persona.FormatPersonalizedResponse("Tôi đã tìm thấy đơn hàng của bạn.", "Phong");
            Assert.StartsWith("Dạ", formatted);
            Assert.Contains("Phong", formatted);
        }

        [Fact]
        public void UserPersona_DetectsCasualPronoun_MayTao()
        {
            var persona = new UserPersona();
            persona.RecordUtterance("Mày xem giùm tao cái đơn này");

            Assert.Equal("tao", persona.UserPronoun);
            Assert.Equal("mày", persona.BotPronoun);
            Assert.Equal(CommunicationTone.Casual, persona.Tone);

            string formatted = persona.FormatPersonalizedResponse("Đã kiểm tra xong đơn hàng.");
            Assert.EndsWith("nhé!", formatted);
        }

        [Fact]
        public void UserPersona_TracksTopicFrequencies_IdentifiesDominantDomain()
        {
            var persona = new UserPersona();

            persona.RecordInteraction("ERP_QUERY_SALES_ORDER");
            persona.RecordInteraction("ERP_QUERY_SALES_ORDER");
            persona.RecordInteraction("ERP_QUERY_SALES_ORDER");
            persona.RecordInteraction("ERP_CHECK_INVENTORY");

            Assert.Equal("ERP_QUERY_SALES_ORDER", persona.DominantDomain);
            Assert.Equal(3, persona.TopicFrequencies["ERP_QUERY_SALES_ORDER"]);
            Assert.Equal(1, persona.TopicFrequencies["ERP_CHECK_INVENTORY"]);
        }

        [Fact]
        public void UserPersona_GeneratesPersonaPromptSummary_ForReActContext()
        {
            var persona = new UserPersona();
            persona.RecordUtterance("Em kiểm tra giúp anh nhé");
            persona.RecordInteraction("ERP_QUERY_SALES_ORDER");

            string summary = persona.GetPersonaPromptSummary("Phong Võ");

            Assert.Contains("[USER PERSONA & HISTORICAL PREFERENCES]", summary);
            Assert.Contains("Phong Võ", summary);
            Assert.Contains("anh", summary);
            Assert.Contains("em", summary);
            Assert.Contains("ERP_QUERY_SALES_ORDER", summary);
        }

        [Fact]
        public async Task ZeroDialogEngine_IntegratesPersonaTracking_EndToEnd()
        {
            var engine = new ZeroDialogEngine();

            var intent = new DialogueIntent("CHECK_STATUS", "Kiểm tra hệ thống")
                .AddSamples("kiểm tra hệ thống", "status");
            intent.ActionHandler = _ => Task.FromResult("Hệ thống hoạt động bình thường.");
            engine.Dst.RegisterIntent(intent);

            var profile = new UserProfile("user_01", "Anh Ba", UserRole.Operator);

            // Turn 1: User speaks with respectful pronoun
            var resp1 = await engine.ChatAsync("session_persona", "Em ơi kiểm tra hệ thống giúp anh với", profile);

            Assert.Equal("anh", profile.Persona.UserPronoun);
            Assert.Equal("em", profile.Persona.BotPronoun);
            Assert.Equal(1, profile.Persona.TopicFrequencies["CHECK_STATUS"]);

            // Turn 2: Follow-up interaction keeps memory active
            var resp2 = await engine.ChatAsync("session_persona", "Kiểm tra hệ thống", profile);
            Assert.Equal(2, profile.Persona.TopicFrequencies["CHECK_STATUS"]);
        }
    }
}
