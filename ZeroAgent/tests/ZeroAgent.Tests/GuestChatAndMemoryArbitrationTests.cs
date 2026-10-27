using System;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    public class GuestChatAndMemoryArbitrationTests
    {
        [Fact]
        public async Task GuestChat_AutoDetectsGuestSession_RestrictsInternalPermissionsWithFriendlyPrompt()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate);

            string guestSessionId = "guest_visitor_01";

            // Guest asks an internal protected action (STOP_MACHINE requires STOP_MACHINE permission)
            var resp = await bot.ChatAsync(guestSessionId, "Dừng máy CNC-01");

            Assert.Equal(SessionState.ActionBlockedByPermission, resp.State);
            Assert.False(resp.IsActionExecuted);
            Assert.Contains("Bạn đang ở chế độ Khách", resp.Text);
            Assert.Contains("yêu cầu xác thực tài khoản", resp.Text);

            var profile = bot.Memory.Profiles.GetOrCreate(guestSessionId);
            Assert.True(profile.IsGuest);
            Assert.Equal(UserRole.Guest, profile.Role);
        }

        [Fact]
        public async Task GuestChat_AllowsPublicKnowledgeQuery_WithoutLogin()
        {
            var bot = IndustrialDialogFactory.CreateIndustrialBot();
            string guestSessionId = "anon_visitor_02";

            // Guest asks about public SOP manual
            var resp = await bot.ChatAsync(guestSessionId, "Quy trình xử lý quá nhiệt Lò nung F-01");

            Assert.Equal(SessionState.Idle, resp.State);
            Assert.Equal("KNOWLEDGE_RETRIEVAL", resp.IntentName);
            Assert.Contains("Quy trình xử lý quá nhiệt Lò nung F-01", resp.Text);
        }

        [Fact]
        public async Task GuestChat_AdaptsSalutationLocally_WithoutCrossGuestContamination()
        {
            var bot = IndustrialDialogFactory.CreateIndustrialBot();
            string guest1 = "guest_user_1";
            string guest2 = "guest_user_2";

            // Guest 1 greets with 'Anh'
            var r1 = await bot.ChatAsync(guest1, "Anh ơi tư vấn giúp anh quy trình bôi trơn máy");
            var profile1 = bot.Memory.Profiles.GetOrCreate(guest1);

            Assert.Equal("anh", profile1.Persona.UserPronoun);
            Assert.Equal("em", profile1.Persona.BotPronoun);
            Assert.Equal(CommunicationTone.Respectful, profile1.Persona.Tone);

            // Guest 2 arrives in a separate session without salutations
            var r2 = await bot.ChatAsync(guest2, "Tài liệu bôi trơn bạc đạn máy CNC ở đâu?");
            var profile2 = bot.Memory.Profiles.GetOrCreate(guest2);

            // Verify Guest 2 is completely isolated and retains neutral 'bạn'
            Assert.Equal("bạn", profile2.Persona.UserPronoun);
            Assert.Equal("tôi", profile2.Persona.BotPronoun);
            Assert.Equal(CommunicationTone.Formal, profile2.Persona.Tone);
        }

        [Fact]
        public async Task GuestChat_InFlightSessionUpgrade_PreservesHistoryAndSlots()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate);
            string guestSessionId = "guest_handover_01";

            // Step 1: Guest tries to stop machine -> blocked by guest role
            var r1 = await bot.ChatAsync(guestSessionId, "Dừng máy CNC-01");
            Assert.Equal(SessionState.ActionBlockedByPermission, r1.State);
            Assert.Contains("Bạn đang ở chế độ Khách", r1.Text);

            var session = bot.GetOrCreateSession(guestSessionId);
            Assert.Equal("CNC-01", session.Slots["machine_id"]);

            // Step 2: User logs in on front-end -> backend executes session upgrade
            var supervisorProfile = new UserProfile("sup_999", "Trần Quản Đốc", UserRole.Supervisor);
            bot.UpgradeGuestSession(guestSessionId, supervisorProfile);

            var activeProfile = bot.ResolveProfile(guestSessionId);
            Assert.False(activeProfile.IsGuest);
            Assert.Equal(UserRole.Supervisor, activeProfile.Role);
            Assert.Equal("Trần Quản Đốc", activeProfile.Name);

            // Step 3: User continues dialogue -> action executes successfully with preserved slot!
            var r2 = await bot.ChatAsync(guestSessionId, "Đã đăng nhập rồi, tiếp tục dừng máy đó đi", supervisorProfile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.True(r2.IsActionExecuted);
            Assert.Contains("CNC-01", r2.Text);
        }

        [Fact]
        public async Task MemoryArbitration_PreventsFalseIntentTrigger_FromSubstringOverlap()
        {
            var bot = IndustrialDialogFactory.CreateIndustrialBot();
            string sessionId = "test_arbitration_01";

            // Query has 'quá nhiệt' and 'CNC-01' which contains 'nhiệt' and machine name.
            // But starts with 'Sự cố... trước đây', which is explicitly an episodic memory query.
            var resp = await bot.ChatAsync(sessionId, "Sự cố quá nhiệt máy CNC-01 trước đây xử lý sao?");

            // Arbitration matrix must ensure HISTORICAL_EPISODE wins over CHECK_TEMPERATURE
            Assert.Equal("HISTORICAL_EPISODE", resp.IntentName);
            Assert.Contains("Ghi nhận sự cố trước đây", resp.Text);
            Assert.Contains("kẹt cánh quạt làm mát", resp.Text);
        }

        [Fact]
        public async Task MemoryArbitration_PreservesSlotCollection_DuringUnrelatedDigression()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate);
            string sessionId = "test_slot_digression";

            var supervisor = new UserProfile(sessionId, "SupervisorHoa", UserRole.Supervisor);

            // Turn 1: Trigger intent without machine_id slot
            var r1 = await bot.ChatAsync(sessionId, "Tôi muốn dừng máy", supervisor);
            Assert.Equal(SessionState.CollectingSlots, r1.State);
            Assert.Equal("machine_id", bot.GetOrCreateSession(sessionId).PendingRequiredSlot);

            // Turn 2: User digresses with a knowledge question containing 'quy trình'
            var r2 = await bot.ChatAsync(sessionId, "Quy trình xử lý quá nhiệt Lò nung F-01 là gì?", supervisor);
            Assert.Equal(SessionState.Idle, r2.State);
            Assert.Equal("KNOWLEDGE_RETRIEVAL", r2.IntentName);
            Assert.Contains("Lò nung F-01", r2.Text);

            // Turn 3: User resumes and provides slot
            var r3 = await bot.ChatAsync(sessionId, "Dừng máy CNC-02", supervisor);
            Assert.Equal(SessionState.Completed, r3.State);
            Assert.True(r3.IsActionExecuted);
            Assert.Contains("CNC-02", r3.Text);
        }
    }
}
