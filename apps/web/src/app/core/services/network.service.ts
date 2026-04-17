import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface SwitchDevice {
  id: number;
  hostname: string;
  site: string;
  role: string;
  management_ip: string;
  loopback_ip?: string;
  last_ping_ok: boolean | null;
  last_ping_ms: number | null;
  last_ssh_ok?: boolean | null;
  picos_version?: string;
}

export interface DeviceRecord {
  id: number;
  switch_name: string;
  building: string;
  device_type: string;
  primary_ip: string;
  management_ip: string;
  status: string;
  asn: string;
  region: string;
}

export interface BgpPeer {
  id: number;
  hostname: string;
  local_as: string;
  router_id: string;
  neighbor_count?: number;
  network_count?: number;
}

export interface BgpNeighbor {
  id: number;
  bgp_id: number;
  neighbor_ip: string;
  remote_as: string;
  bfd?: boolean;
  description?: string;
}

export interface BgpNetwork {
  id: number;
  bgp_id: number;
  prefix: string;
  description?: string;
}

export interface SwitchInterface {
  id: number;
  switch_id: number;
  interface_name: string;
  description?: string;
  speed?: string;
  vlan?: number | string;
  mode?: string;
  admin_status?: string;
}

export interface ConfigVersion {
  id: number;
  switch_id: number;
  version: number;
  created_at: string;
  byte_count?: number;
  diff_summary?: string;
}

/** Generic shape returned by /api/links/{p2p|b2b|fw} — fields are loose because
 *  the desktop has 30+ link columns and we don't want to enumerate them all. */
export interface NetworkLink {
  id: number;
  description?: string;
  switch_a?: string;
  port_a?: string;
  switch_b?: string;
  port_b?: string;
  vlan?: number | string;
  link_type?: string;
  status?: string;
  [key: string]: any;
}

export type LinkKind = 'p2p' | 'b2b' | 'fw';

@Injectable({ providedIn: 'root' })
export class NetworkService {
  private readonly base = environment.centralApiUrl;

  constructor(private http: HttpClient, private auth: AuthService) {}

  // ── Switches ────────────────────────────────────────────────────────────

  getSwitches(): Observable<SwitchDevice[]> {
    return this.http.get<SwitchDevice[]>(`${this.base}/api/switches`);
  }

  getSwitch(id: number): Observable<any> {
    return this.http.get<any>(`${this.base}/api/switches/${id}`);
  }

  getSwitchInterfaces(id: number): Observable<SwitchInterface[]> {
    return this.http.get<SwitchInterface[]>(`${this.base}/api/switches/${id}/interfaces`);
  }

  getSwitchConfigVersions(id: number): Observable<ConfigVersion[]> {
    return this.http.get<ConfigVersion[]>(`${this.base}/api/switches/${id}/config-versions`);
  }

  pingSwitch(id: number): Observable<any> {
    return this.http.post(`${this.base}/api/ssh/${id}/ping`, null);
  }

  downloadSwitchConfig(id: number): Observable<any> {
    return this.http.post(`${this.base}/api/ssh/${id}/download-config`, null);
  }

  // ── Devices (IPAM / switch_guide) ──────────────────────────────────────

  getDevices(): Observable<DeviceRecord[]> {
    return this.http.get<DeviceRecord[]>(`${this.base}/api/devices`);
  }

  getDevice(id: number): Observable<any> {
    return this.http.get<any>(`${this.base}/api/devices/${id}`);
  }

  saveDevice(id: number | null, body: Record<string, any>): Observable<{ id: number }> {
    return id
      ? this.http.put<{ id: number }>(`${this.base}/api/devices/${id}`, body)
      : this.http.post<{ id: number }>(`${this.base}/api/devices`, body);
  }

  deleteDevice(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/devices/${id}`);
  }

  // ── BGP ─────────────────────────────────────────────────────────────────

  getBgpPeers(): Observable<BgpPeer[]> {
    return this.http.get<BgpPeer[]>(`${this.base}/api/bgp`);
  }

  getBgpNeighbors(bgpId: number): Observable<BgpNeighbor[]> {
    return this.http.get<BgpNeighbor[]>(`${this.base}/api/bgp/${bgpId}/neighbors`);
  }

  getBgpNetworks(bgpId: number): Observable<BgpNetwork[]> {
    return this.http.get<BgpNetwork[]>(`${this.base}/api/bgp/${bgpId}/networks`);
  }

  // ── Links (P2P / B2B / FW) ─────────────────────────────────────────────

  getLinks(kind: LinkKind): Observable<NetworkLink[]> {
    return this.http.get<NetworkLink[]>(`${this.base}/api/links/${kind}`);
  }

  saveLink(kind: LinkKind, id: number | null, body: Record<string, any>): Observable<{ id: number }> {
    return id
      ? this.http.put<{ id: number }>(`${this.base}/api/links/${kind}/${id}`, body)
      : this.http.post<{ id: number }>(`${this.base}/api/links/${kind}`, body);
  }

  deleteLink(kind: LinkKind, id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/links/${kind}/${id}`);
  }
}
