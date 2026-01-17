using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using PartyPlanner.Models;
using System.Numerics;

namespace PartyPlanner.Helpers;

/// <summary>
/// Handles rendering of individual event rows in the UI.
/// </summary>
public static class EventRenderer
{
    // Static color constants to avoid allocations
    private static readonly Vector4 ColorGreen = new(0.0742f, 0.530f, 0.150f, 1.0f);
    private static readonly Vector4 ColorPurple = new(0.668f, 0.146f, 0.910f, 1.0f);
    private static readonly Vector4 ColorBlue = new(0.156f, 0.665f, 0.920f, 1.0f);
    private static readonly Vector4 ColorLightGreen = new(0.0888f, 0.740f, 0.176f, 1.0f);

    /// <summary>
    /// Renders a single event row with all details and interactive elements.
    /// </summary>
    /// <param name="ev">The event to render</param>
    /// <param name="cached">Pre-computed cached strings for this event</param>
    public static void DrawEventRow(EventType ev, CachedEventStrings cached)
    {
        ImGui.Spacing();

        // Event title (clickable, opens partake.gg)
        ImGui.TextColored(ColorPurple, ev.Title);
        if (ImGui.IsItemClicked())
        {
            Util.OpenLink("https://www.partake.gg/events/{0}".Format(ev.Id));
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Click to open the partake.gg website.");
            ImGui.EndTooltip();
        }

        // Location (selectable, copies to clipboard)
        ImGui.Text("Location:");
        ImGui.SameLine();
        if (ImGui.Selectable(cached.Location))
        {
            ImGui.SetClipboardText(cached.Location);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextColored(ColorGreen, cached.Location);
            ImGui.Text("Click to copy");
            ImGui.EndTooltip();
        }

        // Start time (humanized with tooltip showing exact time)
        ImGui.Text(string.Format("Starts {0}", cached.StartsAtHumanized));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.SetTooltip(cached.StartsAtLocal);
            ImGui.EndTooltip();
        }
        ImGui.SameLine();

        // End time (humanized with tooltip showing exact time)
        ImGui.Text(string.Format("|  Ends {0}", cached.EndsAtHumanized));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.SetTooltip(cached.EndsAtLocal);
            ImGui.EndTooltip();
        }

        // Full time range
        ImGui.TextColored(ColorBlue,
           string.Format("From {0} to {1}", cached.StartsAtLocal, cached.EndsAtLocal));

        // Tags
        ImGui.TextColored(ColorLightGreen,
            string.Format("Tags: {0}", cached.FormattedTags));

        // Description (collapsible)
        if (ImGui.CollapsingHeader("More details"))
        {
            ImGui.TextWrapped(ev.Description);
        }
    }
}
