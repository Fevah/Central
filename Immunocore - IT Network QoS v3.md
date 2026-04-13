QoS Configuration Guide for MEP-91 and MEP-92 Core Switches
Overview
This document provides a comprehensive guide to the Quality of Service (QoS) configuration implemented across MEP-91 and MEP-92 core switches. The configuration uses a hybrid QoS strategy that combines two scheduler profiles optimized for different port types.

Hybrid QoS Strategy
The hybrid approach addresses the unique requirements of different port types:

Profile	Target Ports	Strategy	Key Features
qos-flex-profile	Core/Trunk ports (10G+)	Weight-only WFQ	Maximum flexibility, proportional bandwidth sharing
qos-1g-user-profile	1G User/Access ports	WFQ + Guaranteed minimums	Failsafe protection for critical traffic
Why Hybrid?

10G+ ports have abundant bandwidth; proportional allocation via weights is sufficient
1G ports are constrained; guaranteed minimums ensure critical applications always get baseline bandwidth during congestion
Section 1: Forwarding Classes
Forwarding classes define traffic categories and map them to hardware queue priorities.

Configuration
set class-of-service forwarding-class fc-best-effort
set class-of-service forwarding-class fc-bulk local-priority 1
set class-of-service forwarding-class fc-network-control local-priority 7
set class-of-service forwarding-class fc-realtime local-priority 4
set class-of-service forwarding-class fc-signaling local-priority 3
set class-of-service forwarding-class fc-transactional local-priority 2
set class-of-service forwarding-class fc-video local-priority 5
set class-of-service forwarding-class fc-voice-ef local-priority 6
Detailed Explanation
Forwarding Class	Local Priority	Purpose	Typical Traffic
fc-network-control	7 (Highest)	Network infrastructure	BGP, OSPF, VRRP, LLDP, BFD, spanning-tree
fc-voice-ef	6	Voice traffic	VoIP RTP streams, Avaya voice
fc-video	5	Video conferencing	Teams, Zoom, video streaming
fc-realtime	4	Real-time applications	Interactive apps, gaming, live feeds
fc-signaling	3	Call signaling	SIP, H.323, call setup/teardown
fc-transactional	2	Business-critical data	Database queries, ERP, financial transactions
fc-bulk	1	Background transfers	Backups, replication, large file transfers
fc-best-effort	0 (Lowest)	Default traffic	Web browsing, email, general internet
Key Points:

Local priority maps directly to hardware queue priority (0-7)
Higher priority = preferential treatment during congestion
fc-best-effort has no explicit priority (defaults to 0)
Section 2: DSCP Classifier
The DSCP classifier maps incoming packets to forwarding classes based on their DSCP markings.

Configuration
set class-of-service classifier qos-dscp-classifier trust-mode "dscp"

# Network Control (CS6, CS7)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-network-control code-point 48
set class-of-service classifier qos-dscp-classifier forwarding-class fc-network-control code-point 56

# Voice (VA, EF)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-voice-ef code-point 44
set class-of-service classifier qos-dscp-classifier forwarding-class fc-voice-ef code-point 46

# Video (AF41, AF42, AF43, CS4)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 34
set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 36
set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 38
set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 40

# Realtime (AF31, AF32, AF33, CS3)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 26
set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 28
set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 30
set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 32

# Signaling (AF21, AF22, AF23, CS2)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 18
set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 20
set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 22
set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 24

# Transactional (AF11, AF12, AF13, CS1)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 10
set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 12
set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 14
set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 16

# Bulk (Low-priority marking + CS1)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-bulk code-point 1
set class-of-service classifier qos-dscp-classifier forwarding-class fc-bulk code-point 8

# Best Effort (BE/DF)
set class-of-service classifier qos-dscp-classifier forwarding-class fc-best-effort code-point 0
DSCP Code Point Reference
Forwarding Class	DSCP Values	DSCP Names	Binary
fc-network-control	48, 56	CS6, CS7	110000, 111000
fc-voice-ef	44, 46	VA, EF	101100, 101110
fc-video	34, 36, 38, 40	AF41, AF42, AF43, CS4	100010-101000
fc-realtime	26, 28, 30, 32	AF31, AF32, AF33, CS3	011010-100000
fc-signaling	18, 20, 22, 24	AF21, AF22, AF23, CS2	010010-011000
fc-transactional	10, 12, 14, 16	AF11, AF12, AF13, CS1	001010-010000
fc-bulk	1, 8	Low priority, CS1	000001, 001000
fc-best-effort	0	BE/DF (Default)	000000
Trust Mode:

trust-mode "dscp" means the switch honors incoming DSCP markings
Traffic is classified based on existing DSCP values (end-to-end QoS preserved)
Section 3: Scheduler Profiles
Two scheduler profiles provide different bandwidth allocation strategies.

3.1 qos-flex-profile (Weight-Only for 10G+ Ports)
This profile uses pure WFQ weights without guaranteed rates, allowing maximum flexibility on high-bandwidth links.

Scheduler Definitions
# Strict Priority (SP) Queues - Highest priority, rate-limited
set class-of-service scheduler flex-sched-network-control mode "SP"
set class-of-service scheduler flex-sched-network-control max-bandwidth-pps 2000
set class-of-service scheduler flex-sched-voice mode "SP"
set class-of-service scheduler flex-sched-voice max-bandwidth-pps 10000

# Weighted Fair Queuing (WFQ) Queues - Proportional sharing
set class-of-service scheduler flex-sched-video mode "WFQ"
set class-of-service scheduler flex-sched-video weight 30
set class-of-service scheduler flex-sched-realtime mode "WFQ"
set class-of-service scheduler flex-sched-realtime weight 25
set class-of-service scheduler flex-sched-transactional mode "WFQ"
set class-of-service scheduler flex-sched-transactional weight 20
set class-of-service scheduler flex-sched-signaling mode "WFQ"
set class-of-service scheduler flex-sched-signaling weight 10
set class-of-service scheduler flex-sched-best-effort mode "WFQ"
set class-of-service scheduler flex-sched-best-effort weight 10
set class-of-service scheduler flex-sched-bulk mode "WFQ"
set class-of-service scheduler flex-sched-bulk weight 5
Profile Binding
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-network-control scheduler "flex-sched-network-control"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-voice-ef scheduler "flex-sched-voice"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-video scheduler "flex-sched-video"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-realtime scheduler "flex-sched-realtime"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-signaling scheduler "flex-sched-signaling"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-transactional scheduler "flex-sched-transactional"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-bulk scheduler "flex-sched-bulk"
set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-best-effort scheduler "flex-sched-best-effort"
Bandwidth Allocation (qos-flex-profile)
Class	Mode	Weight	% of WFQ Pool	Rate Limit
Network Control	SP	-	(Priority)	2,000 pps
Voice	SP	-	(Priority)	10,000 pps
Video	WFQ	30	30%	-
Realtime	WFQ	25	25%	-
Transactional	WFQ	20	20%	-
Signaling	WFQ	10	10%	-
Best-Effort	WFQ	10	10%	-
Bulk	WFQ	5	5%	-
Total WFQ		100	100%	
3.2 qos-1g-user-profile (WFQ + Guaranteed Rates for 1G Ports)
This profile adds failsafe guaranteed minimum rates for critical traffic classes, ensuring baseline service during severe congestion.

Scheduler Definitions
# Strict Priority (SP) Queues - Same as flex profile
set class-of-service scheduler 1g-sched-network-control mode "SP"
set class-of-service scheduler 1g-sched-network-control max-bandwidth-pps 2000
set class-of-service scheduler 1g-sched-voice mode "SP"
set class-of-service scheduler 1g-sched-voice max-bandwidth-pps 10000

# WFQ Queues WITH guaranteed rates for critical classes
set class-of-service scheduler 1g-sched-video mode "WFQ"
set class-of-service scheduler 1g-sched-video weight 30
set class-of-service scheduler 1g-sched-video guaranteed-rate 50000

set class-of-service scheduler 1g-sched-realtime mode "WFQ"
set class-of-service scheduler 1g-sched-realtime weight 25
set class-of-service scheduler 1g-sched-realtime guaranteed-rate 30000

set class-of-service scheduler 1g-sched-transactional mode "WFQ"
set class-of-service scheduler 1g-sched-transactional weight 20
set class-of-service scheduler 1g-sched-transactional guaranteed-rate 30000

set class-of-service scheduler 1g-sched-signaling mode "WFQ"
set class-of-service scheduler 1g-sched-signaling weight 10
set class-of-service scheduler 1g-sched-signaling guaranteed-rate 10000

# No guaranteed rates for lower-priority classes
set class-of-service scheduler 1g-sched-best-effort mode "WFQ"
set class-of-service scheduler 1g-sched-best-effort weight 10
set class-of-service scheduler 1g-sched-bulk mode "WFQ"
set class-of-service scheduler 1g-sched-bulk weight 5
Profile Binding
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-network-control scheduler "1g-sched-network-control"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-voice-ef scheduler "1g-sched-voice"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-video scheduler "1g-sched-video"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-realtime scheduler "1g-sched-realtime"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-signaling scheduler "1g-sched-signaling"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-transactional scheduler "1g-sched-transactional"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-bulk scheduler "1g-sched-bulk"
set class-of-service scheduler-profile qos-1g-user-profile forwarding-class fc-best-effort scheduler "1g-sched-best-effort"
Bandwidth Allocation (qos-1g-user-profile on 1G Port)
Class	Mode	Weight	% of WFQ	Guaranteed Rate	% of 1G
Network Control	SP	-	(Priority)	N/A	pps-limited
Voice	SP	-	(Priority)	N/A	pps-limited
Video	WFQ	30	30%	50 Mbps	5%
Realtime	WFQ	25	25%	30 Mbps	3%
Transactional	WFQ	20	20%	30 Mbps	3%
Signaling	WFQ	10	10%	10 Mbps	1%
Best-Effort	WFQ	10	10%	None	-
Bulk	WFQ	5	5%	None	-
Total Guaranteed				120 Mbps	12%
3.3 How SP, WFQ, and Guaranteed Rates Work Together
Processing Order
SP Queues First: Network-control and voice queues are serviced first (strict priority)
Rate-limited to prevent starvation of other queues
Network-control: max 2,000 packets/sec
Voice: max 10,000 packets/sec
Guaranteed Rates Next: If a WFQ queue has a guaranteed rate configured, it receives at least that bandwidth when it has traffic
WFQ Weights Last: Remaining bandwidth is distributed proportionally by weight
Example Scenario (1G Port with qos-1g-user-profile)
During normal operation (light load):

All traffic classes get bandwidth on demand
No queuing or drops
During congestion (heavy load):

Voice and Network-control served first (up to pps limits)
Video guaranteed 50 Mbps minimum
Realtime guaranteed 30 Mbps minimum
Transactional guaranteed 30 Mbps minimum
Signaling guaranteed 10 Mbps minimum
Remaining bandwidth (~880 Mbps minus SP traffic) split by weights
Best-effort and bulk only get surplus bandwidth
Key Insight: Guaranteed rates are failsafe minimums, not allocations. Traffic classes can exceed their guarantees when bandwidth is available.

Section 4: Priority Flow Control (PFC) Profile
PFC enables lossless transport for specific traffic classes, critical for storage replication.

Configuration
set class-of-service pfc-profile pfc-storage code-point 0 drop true
set class-of-service pfc-profile pfc-storage code-point 1 drop false
set class-of-service pfc-profile pfc-storage code-point 2 drop true
set class-of-service pfc-profile pfc-storage code-point 3 drop true
set class-of-service pfc-profile pfc-storage code-point 4 drop true
set class-of-service pfc-profile pfc-storage code-point 5 drop true
set class-of-service pfc-profile pfc-storage code-point 6 drop true
set class-of-service pfc-profile pfc-storage code-point 7 drop true
Explanation
Code Point	Drop	Meaning
0	true	Best-effort - drops allowed
1	false	Bulk/Storage - NO drops (lossless)
2-7	true	Other classes - drops allowed
Why Lossless for Bulk/Storage?

Storage replication (iSCSI, NFS, backup) cannot tolerate packet loss
PFC sends pause frames to upstream switch when queue fills
Prevents drops at the cost of potential head-of-line blocking
Only enabled for code-point 1 (fc-bulk class)
Usage: Apply pfc-storage profile to storage-facing interfaces.

Section 5: Traffic Policers
Policers provide ingress rate limiting to prevent specific traffic types from overwhelming the network.

Configuration
# Bulk Traffic Policer
set firewall policer bulk-limit if-exceeding count-mode packet
set firewall policer bulk-limit if-exceeding rate-limit 50000
set firewall policer bulk-limit if-exceeding burst-limit 10000
set firewall policer bulk-limit then action discard

# Video Traffic Policer
set firewall policer video-limit if-exceeding count-mode packet
set firewall policer video-limit if-exceeding rate-limit 100000
set firewall policer video-limit if-exceeding burst-limit 20000
set firewall policer video-limit then action discard
Policer Details
Policer	Rate Limit	Burst Limit	Action	Purpose
bulk-limit	50,000 pps	10,000 packets	Discard	Prevent backup traffic from flooding network
video-limit	100,000 pps	20,000 packets	Discard	Cap video streaming bandwidth
When to Use:

Apply policers to interfaces where untrusted or high-volume traffic ingresses
Protects network from traffic bursts and bandwidth abuse
Section 6: Interface QoS Bindings
Applying Profiles to Interfaces
Core/Trunk Ports (10G+) - Use qos-flex-profile
set class-of-service interface xe-1/1/1 classifier "qos-dscp-classifier"
set class-of-service interface xe-1/1/1 scheduler-profile "qos-flex-profile"
1G User/Access Ports - Use qos-1g-user-profile
set class-of-service interface ge-0/0/1 classifier "qos-dscp-classifier"
set class-of-service interface ge-0/0/1 scheduler-profile "qos-1g-user-profile"
Storage Ports - Add PFC Profile
set class-of-service interface xe-1/1/5 classifier "qos-dscp-classifier"
set class-of-service interface xe-1/1/5 scheduler-profile "qos-flex-profile"
set class-of-service interface xe-1/1/5 pfc-profile "pfc-storage"
Current Interface Assignments
All xe-1/1/X interfaces (ports 1-32) on all switches are currently configured with:

Classifier: qos-dscp-classifier
Scheduler Profile: qos-flex-profile
Note: To apply qos-1g-user-profile to 1G user ports, modify the scheduler-profile assignment for those specific interfaces.

Section 7: Voice VLAN Configuration
Special handling for Avaya VoIP phones.

Configuration
set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 mask ff:ff:ff:00:00:00
set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 description "Avaya"
set vlans voice-vlan local-priority 6
set vlans voice-vlan dscp 46
Explanation
MAC-based identification: Matches Avaya phones by OUI (c8:1f:ea:66:72:b6 with mask)
Auto-marking: Traffic from matched devices gets:
Local priority: 6 (fc-voice-ef queue)
DSCP: 46 (EF - Expedited Forwarding)
Purpose: Ensures voice traffic is properly prioritized even if phone doesn't mark packets
Section 8: Implementation Guide
Step 1: Verify Current Configuration
show class-of-service interface
show class-of-service scheduler-profile
show class-of-service forwarding-class
Step 2: Apply Profile to Interface
For a new 1G user-facing port:

set class-of-service interface ge-0/0/10 classifier "qos-dscp-classifier"
set class-of-service interface ge-0/0/10 scheduler-profile "qos-1g-user-profile"
For a new 10G uplink:

set class-of-service interface xe-1/1/33 classifier "qos-dscp-classifier"
set class-of-service interface xe-1/1/33 scheduler-profile "qos-flex-profile"
Step 3: Apply PFC for Storage (if needed)
set class-of-service interface xe-1/1/33 pfc-profile "pfc-storage"
Step 4: Apply Policers (if needed)
Reference policers in firewall filter rules applied to specific interfaces.

Section 9: Configuration Files
The following clean configuration files are available:

File	Switch	Description
MEP-91-Core01.md	MEP-91-Core01	Primary core switch, Site 91
MEP-91-Core02.md	MEP-91-Core02	Secondary core switch, Site 91
MEP-92-Core01.md	MEP-92-Core01	Primary core switch, Site 92
MEP-92-Core02.md	MEP-92-Core02	Secondary core switch, Site 92
All configuration files contain the complete QoS configuration (both profiles) and are production-ready.

Section 10: Quick Reference
Weight Distribution Summary
Class	Weight	Priority	Guaranteed (1G)
Network Control	SP	Highest	pps-limited
Voice	SP	High	pps-limited
Video	30	-	50 Mbps
Realtime	25	-	30 Mbps
Transactional	20	-	30 Mbps
Signaling	10	-	10 Mbps
Best-Effort	10	-	None
Bulk	5	Lowest	None
Profile Selection Guide
Port Type	Speed	Profile	Why
Core-to-core links	10G+	qos-flex-profile	High bandwidth, flexibility needed
Firewall links	10G	qos-flex-profile	Consistent policy across link
Server uplinks	10G	qos-flex-profile	High throughput required
User access ports	1G	qos-1g-user-profile	Failsafe guarantees for critical apps
Storage ports	10G	qos-flex-profile + pfc-storage	Lossless for replication
Appendix A: DSCP to Queue Mapping Summary
DSCP 48, 56 (CS6, CS7)     → fc-network-control → Queue 7 (SP)
DSCP 44, 46 (VA, EF)       → fc-voice-ef        → Queue 6 (SP)
DSCP 34-40 (AF4x, CS4)     → fc-video           → Queue 5 (WFQ w=30)
DSCP 26-32 (AF3x, CS3)     → fc-realtime        → Queue 4 (WFQ w=25)
DSCP 18-24 (AF2x, CS2)     → fc-signaling       → Queue 3 (WFQ w=10)
DSCP 10-16 (AF1x, CS1)     → fc-transactional   → Queue 2 (WFQ w=20)
DSCP 1, 8                  → fc-bulk            → Queue 1 (WFQ w=5)
DSCP 0 (BE/DF)             → fc-best-effort     → Queue 0 (WFQ w=10)
Appendix B: Troubleshooting
Check Queue Statistics
show class-of-service interface xe-1/1/1 statistics
Verify DSCP Classification
show class-of-service classifier qos-dscp-classifier
Monitor PFC
show class-of-service pfc-profile pfc-storage
show interface xe-1/1/5 extensive | match pfc
Common Issues
Traffic not getting expected priority
Verify DSCP marking at source
Check classifier is applied to interface
Confirm scheduler-profile assignment
Voice quality issues
Check SP queue utilization (should be low)
Verify pps limits aren't being hit
Ensure voice VLAN DSCP marking is working
Storage replication failures
Confirm PFC profile applied to storage interfaces
Verify PFC is enabled on both ends
Check for PFC pause frame statistics
Document Version: 1.0
Last Updated: February 2026
Applies to: MEP-91-Core01, MEP-91-Core02, MEP-92-Core01, MEP-92-Core02