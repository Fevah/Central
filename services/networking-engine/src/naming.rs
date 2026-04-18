//! Brace-substitution naming templates — port of `LinkNamingService.cs`,
//! `DeviceNamingService.cs`, `ServerNamingService.cs`. All three use the
//! identical brace-substitution algorithm with different token dictionaries;
//! one engine + three context types captures that without duplication.
//!
//! **Semantics (must match the C# byte-for-byte — Phase 5f parity tests
//! compare against the generator output):**
//!
//! - Unknown tokens pass through verbatim (braces included).
//! - Empty values substitute to the empty string.
//! - Unmatched opening brace emits the tail unchanged.

use std::collections::HashMap;

// ─── Shared expander ──────────────────────────────────────────────────────

fn expand(template: &str, tokens: &HashMap<&str, String>) -> String {
    if template.is_empty() { return String::new(); }
    let bytes = template.as_bytes();
    let mut out = String::with_capacity(template.len() + 32);
    let mut i = 0usize;

    while i < bytes.len() {
        // Find next opening brace.
        let mut open: Option<usize> = None;
        let mut j = i;
        while j < bytes.len() {
            if bytes[j] == b'{' { open = Some(j); break; }
            j += 1;
        }
        let Some(open) = open else {
            out.push_str(&template[i..]);
            break;
        };
        out.push_str(&template[i..open]);

        // Find matching close.
        let mut close: Option<usize> = None;
        let mut k = open + 1;
        while k < bytes.len() {
            if bytes[k] == b'}' { close = Some(k); break; }
            k += 1;
        }
        let Some(close) = close else {
            out.push_str(&template[open..]);
            break;
        };

        let name = &template[open + 1..close];
        match tokens.get(name) {
            Some(value) => out.push_str(value),
            None => out.push_str(&template[open..=close]),
        }
        i = close + 1;
    }
    out
}

fn format_instance(instance: Option<i32>, padding: u32) -> String {
    match instance {
        None => String::new(),
        Some(n) if padding == 0 => n.to_string(),
        Some(n) => format!("{:0width$}", n, width = padding as usize),
    }
}

// ─── Link ─────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Default, serde::Serialize, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LinkNamingContext {
    pub site_a: Option<String>,
    pub site_b: Option<String>,
    pub device_a: Option<String>,
    pub device_b: Option<String>,
    pub port_a: Option<String>,
    pub port_b: Option<String>,
    pub role_a: Option<String>,
    pub role_b: Option<String>,
    pub vlan_id: Option<i32>,
    pub subnet: Option<String>,
    pub description: Option<String>,
    pub link_code: Option<String>,
}

pub fn expand_link(template: &str, ctx: &LinkNamingContext) -> String {
    let opt = |v: &Option<String>| v.clone().unwrap_or_default();
    let mut tokens: HashMap<&str, String> = HashMap::new();
    tokens.insert("site_a",      opt(&ctx.site_a));
    tokens.insert("site_b",      opt(&ctx.site_b));
    tokens.insert("device_a",    opt(&ctx.device_a));
    tokens.insert("device_b",    opt(&ctx.device_b));
    tokens.insert("port_a",      opt(&ctx.port_a));
    tokens.insert("port_b",      opt(&ctx.port_b));
    tokens.insert("role_a",      opt(&ctx.role_a));
    tokens.insert("role_b",      opt(&ctx.role_b));
    tokens.insert("vlan",        ctx.vlan_id.map(|v| v.to_string()).unwrap_or_default());
    tokens.insert("subnet",      opt(&ctx.subnet));
    tokens.insert("description", opt(&ctx.description));
    tokens.insert("link_code",   opt(&ctx.link_code));
    expand(template, &tokens)
}

// ─── Device ───────────────────────────────────────────────────────────────

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DeviceNamingContext {
    pub region_code: Option<String>,
    pub site_code: Option<String>,
    pub building_code: Option<String>,
    pub rack_code: Option<String>,
    pub role_code: Option<String>,
    pub instance: Option<i32>,
    #[serde(default = "default_padding")]
    pub instance_padding: u32,
}

impl Default for DeviceNamingContext {
    fn default() -> Self {
        Self {
            region_code: None, site_code: None, building_code: None, rack_code: None,
            role_code: None, instance: None, instance_padding: default_padding(),
        }
    }
}

fn default_padding() -> u32 { 2 }

pub fn expand_device(template: &str, ctx: &DeviceNamingContext) -> String {
    let opt = |v: &Option<String>| v.clone().unwrap_or_default();
    let mut tokens: HashMap<&str, String> = HashMap::new();
    tokens.insert("region_code",   opt(&ctx.region_code));
    tokens.insert("site_code",     opt(&ctx.site_code));
    tokens.insert("building_code", opt(&ctx.building_code));
    tokens.insert("rack_code",     opt(&ctx.rack_code));
    tokens.insert("role_code",     opt(&ctx.role_code));
    tokens.insert("instance",      format_instance(ctx.instance, ctx.instance_padding));
    expand(template, &tokens)
}

// ─── Server ───────────────────────────────────────────────────────────────

#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ServerNamingContext {
    pub region_code: Option<String>,
    pub site_code: Option<String>,
    pub building_code: Option<String>,
    pub rack_code: Option<String>,
    pub profile_code: Option<String>,
    pub instance: Option<i32>,
    #[serde(default = "default_padding")]
    pub instance_padding: u32,
}

impl Default for ServerNamingContext {
    fn default() -> Self {
        Self {
            region_code: None, site_code: None, building_code: None, rack_code: None,
            profile_code: None, instance: None, instance_padding: default_padding(),
        }
    }
}

pub fn expand_server(template: &str, ctx: &ServerNamingContext) -> String {
    let opt = |v: &Option<String>| v.clone().unwrap_or_default();
    let mut tokens: HashMap<&str, String> = HashMap::new();
    tokens.insert("region_code",   opt(&ctx.region_code));
    tokens.insert("site_code",     opt(&ctx.site_code));
    tokens.insert("building_code", opt(&ctx.building_code));
    tokens.insert("rack_code",     opt(&ctx.rack_code));
    tokens.insert("profile_code",  opt(&ctx.profile_code));
    tokens.insert("instance",      format_instance(ctx.instance, ctx.instance_padding));
    expand(template, &tokens)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn empty_template_returns_empty() {
        let ctx = LinkNamingContext::default();
        assert_eq!(expand_link("", &ctx), "");
    }

    #[test]
    fn no_tokens_passes_through() {
        let ctx = LinkNamingContext::default();
        assert_eq!(expand_link("static-text", &ctx), "static-text");
    }

    #[test]
    fn link_tokens_substitute() {
        let ctx = LinkNamingContext {
            device_a: Some("MEP-91-CORE02".into()),
            device_b: Some("MEP-92-CORE01".into()),
            ..Default::default()
        };
        assert_eq!(expand_link("{device_a}_{device_b}", &ctx), "MEP-91-CORE02_MEP-92-CORE01");
    }

    #[test]
    fn unknown_token_passes_through_verbatim() {
        let ctx = LinkNamingContext::default();
        assert_eq!(expand_link("{typo_here}", &ctx), "{typo_here}");
    }

    #[test]
    fn empty_value_substitutes_empty_string() {
        let ctx = LinkNamingContext::default();
        // All fields None → empty values.
        assert_eq!(expand_link("A{device_a}B", &ctx), "AB");
    }

    #[test]
    fn unmatched_open_brace_emits_tail_verbatim() {
        let ctx = LinkNamingContext::default();
        assert_eq!(expand_link("x{unclosed", &ctx), "x{unclosed");
    }

    #[test]
    fn vlan_numeric_to_string() {
        let ctx = LinkNamingContext { vlan_id: Some(101), ..Default::default() };
        assert_eq!(expand_link("vlan-{vlan}", &ctx), "vlan-101");
    }

    #[test]
    fn device_instance_zero_pads_default_2() {
        let ctx = DeviceNamingContext {
            building_code: Some("MEP-91".into()),
            role_code: Some("CORE".into()),
            instance: Some(2),
            ..Default::default()
        };
        assert_eq!(expand_device("{building_code}-{role_code}{instance}", &ctx), "MEP-91-CORE02");
    }

    #[test]
    fn device_instance_padding_0_disables() {
        let ctx = DeviceNamingContext {
            role_code: Some("CORE".into()),
            instance: Some(2),
            instance_padding: 0,
            ..Default::default()
        };
        assert_eq!(expand_device("{role_code}{instance}", &ctx), "CORE2");
    }

    #[test]
    fn device_instance_none_is_empty() {
        let ctx = DeviceNamingContext {
            role_code: Some("CORE".into()),
            instance: None,
            ..Default::default()
        };
        assert_eq!(expand_device("{role_code}{instance}", &ctx), "CORE");
    }

    #[test]
    fn server_tokens() {
        let ctx = ServerNamingContext {
            building_code: Some("MEP-91".into()),
            profile_code: Some("Server4NIC".into()),
            instance: Some(1),
            ..Default::default()
        };
        assert_eq!(expand_server("{building_code}-{profile_code}-{instance}", &ctx),
                   "MEP-91-Server4NIC-01");
    }

    #[test]
    fn multiple_open_braces_no_close_bails_out() {
        let ctx = LinkNamingContext::default();
        assert_eq!(expand_link("x{{y", &ctx), "x{{y");
    }

    #[test]
    fn tokens_in_middle_of_text() {
        let ctx = DeviceNamingContext {
            building_code: Some("MEP-91".into()),
            role_code: Some("CORE".into()),
            instance: Some(2),
            ..Default::default()
        };
        assert_eq!(expand_device("prefix-{building_code}-{role_code}{instance}-suffix", &ctx),
                   "prefix-MEP-91-CORE02-suffix");
    }
}
