using System.Collections.Generic;
using Xunit;
using ZeroPrompt.Core.Templating;

namespace ZeroPrompt.Tests
{
    public class PromptTemplateTests
    {
        [Fact]
        public void PromptTemplate_Should_Render_Variables_And_Formatting()
        {
            var template = PromptTemplate.Parse("Device: {{device_id}}, Status: {{status}}, Code: {{error_code:X4}}");
            var context = new PromptContext()
                .Set("device_id", "PLC_01")
                .Set("status", "ACTIVE")
                .Set("error_code", 255);

            string rendered = template.Render(context);
            Assert.Equal("Device: PLC_01, Status: ACTIVE, Code: 00FF", rendered);
        }

        [Fact]
        public void PromptTemplate_Should_Evaluate_If_Conditions()
        {
            var template = PromptTemplate.Parse("Alarm: {{#if has_alarm}}CRITICAL ALERT: {{alarm_msg}}{{/if}}All systems nominal.");
            
            // Case 1: has_alarm is true
            var ctx1 = new PromptContext()
                .Set("has_alarm", true)
                .Set("alarm_msg", "Overheating detected! ");
            Assert.Equal("Alarm: CRITICAL ALERT: Overheating detected! All systems nominal.", template.Render(ctx1));

            // Case 2: has_alarm is false
            var ctx2 = new PromptContext()
                .Set("has_alarm", false);
            Assert.Equal("Alarm: All systems nominal.", template.Render(ctx2));
        }
    }
}
