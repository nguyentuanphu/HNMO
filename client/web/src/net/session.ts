// The login flow of the client (Scenes/LoginScene, CharacterScene): server list from
// the connect server, then login and character list on the chosen game server.

import { Connection } from './connection.js';
import type { ServerAddress } from './connection.js';
import { read } from './packets.js';
import type { PacketValues } from './packets.js';
import { PACKETS } from './packets.generated.js';
import { parseAppearance } from '../game/items.js';

// The client identity the desktop client sends (Network/Server/WSclient.cpp: Version, Serial).
const CLIENT_VERSION = new TextEncoder().encode('20404');
const CLIENT_SERIAL = new TextEncoder().encode('k1Pk2jcET48mxL3b');
const XOR3_KEYS = [0xfc, 0xcf, 0xab];
const USERNAME_BYTES = 10;
const PASSWORD_BYTES = 20;
const LOGIN_OK = 1;

/** A game server of the connect server's list. */
export interface ServerListEntry {
  id: number;
  /** Load in percent. */
  load: number;
}

/** A character of the account's character list. */
export interface CharacterListEntry {
  slot: number;
  name: string;
  level: number;
  /** Index into CLASSES. */
  classIndex: number;
  /** A third class (the desktop's IsMasterLevel): the server's class number is odd for those. */
  master?: boolean;
  equipment: ReturnType<typeof parseAppearance>['equipment'];
}

// The answers this file reads, with the fields it uses (packets.generated.js has them all).
interface ServerListResponse extends PacketValues { Servers: { ServerId: number; LoadPercentage: number }[] }
interface ConnectionInfo extends PacketValues { IpAddress: string; Port: number }
interface GameServerEntered extends PacketValues { PlayerId: number }
interface LoginResponse extends PacketValues { Success: number }
interface CharacterList extends PacketValues {
  Characters: { SlotIndex: number; Name: string; Level: number; Appearance: Uint8Array }[];
}

/** The answer to CreateCharacter. */
export interface CharacterCreated extends PacketValues {
  Success: boolean;
  CharacterName: string;
  CharacterSlot: number;
  Level: number;
  Class: number;
}

/** The character's state on entering the world (CharacterInformationExtended); more fields than these are read. */
export interface CharacterInformation extends PacketValues {
  X: number;
  Y: number;
  MapId: number;
}

const LOGIN_RESULTS: Record<number, string> = {
  0: 'Wrong password.',
  1: 'OK',
  2: 'That account doesn\'t exist.',
  3: 'This account is already logged in.',
  4: 'The server is full.',
  5: 'This account is blocked.',
  6: 'Your client version is too old for this server.',
  8: 'Too many failed logins. Try again later.',
};

function xor3Field(text: string, length: number): Uint8Array {
  const bytes = new Uint8Array(length);
  bytes.set(new TextEncoder().encode(text).subarray(0, length));
  for (let i = 0; i < length; i++) bytes[i] ^= XOR3_KEYS[i % 3];
  return bytes;
}

function packetOf(group: string, name: string): [code: number, subCode: number | undefined] {
  const packet = PACKETS[group].packets[name];
  return [packet.code, packet.hasSubCode ? packet.subCode : undefined];
}

// Asks the connect server for its game servers.
export async function fetchServerList(relayUrl: string, connectServer: ServerAddress): Promise<ServerListEntry[]> {
  const connection = await new Connection(relayUrl, { ...connectServer, encrypted: false }).open();
  try {
    await connection.waitFor(...packetOf('ConnectServer', 'Hello'));
    const list = await connection.request<ServerListResponse>('ConnectServer', 'ServerListRequest', {}, 'ConnectServer', 'ServerListResponse');
    return list.Servers.map((server) => ({ id: server.ServerId, load: server.LoadPercentage }));
  } finally {
    connection.close();
  }
}

export async function fetchGameServerAddress(relayUrl: string, connectServer: ServerAddress, serverId: number): Promise<ServerAddress> {
  const connection = await new Connection(relayUrl, { ...connectServer, encrypted: false }).open();
  try {
    await connection.waitFor(...packetOf('ConnectServer', 'Hello'));
    const info = await connection.request<ConnectionInfo>('ConnectServer', 'ConnectionInfoRequest', { ServerId: serverId }, 'ConnectServer', 'ConnectionInfo');
    return { host: info.IpAddress, port: info.Port };
  } finally {
    connection.close();
  }
}

// Opens the game server connection and logs in. Resolves with the open connection.
export async function login(relayUrl: string, gameServer: ServerAddress, username: string, password: string): Promise<Connection> {
  const connection = new Connection(relayUrl, { ...gameServer, encrypted: true });
  const entered = connection.waitFor(...packetOf('ServerToClient', 'GameServerEntered'));
  await connection.open();
  const welcome = read<GameServerEntered>('ServerToClient', 'GameServerEntered', await entered);
  const response = await connection.request<LoginResponse>('ClientToServer', 'LoginLongPassword', {
    Username: xor3Field(username, USERNAME_BYTES),
    Password: xor3Field(password, PASSWORD_BYTES),
    TickCount: Math.floor(performance.now()) >>> 0,
    ClientVersion: CLIENT_VERSION,
    ClientSerial: CLIENT_SERIAL,
  }, 'ServerToClient', 'LoginResponse');
  if (response.Success !== LOGIN_OK) {
    connection.close();
    throw new Error(LOGIN_RESULTS[response.Success] ?? `Login refused (code ${response.Success}).`);
  }
  connection.playerId = welcome.PlayerId;
  return connection;
}

// Character classes by the server's class number >> 2, with the body set the client draws.
export const CLASSES = ['Dark Wizard', 'Dark Knight', 'Fairy Elf', 'Magic Gladiator', 'Dark Lord', 'Summoner', 'Rage Fighter'];

export function classOfAppearance(appearance: ArrayLike<number> | undefined): number {
  const baseClass = (appearance?.[0] ?? 0) >> 2;
  return Math.min(baseClass, CLASSES.length - 1);
}

export async function fetchCharacters(connection: Connection): Promise<CharacterListEntry[]> {
  const list = await connection.request<CharacterList>('ClientToServer', 'RequestCharacterList', { Language: 0 }, 'ServerToClient', 'CharacterListExtended');
  return list.Characters.map((character) => ({
    slot: character.SlotIndex,
    name: character.Name,
    level: character.Level,
    classIndex: classOfAppearance(character.Appearance),
    master: ((character.Appearance?.[0] ?? 0) & 1) === 1,
    equipment: parseAppearance(character.Appearance).equipment,
  }));
}

// Creates a character (CharacterScene / CharMakeWin). The class goes as the server's class
// number, four apart for each base class. Classes the account hasn't unlocked yet
// (e.g. Magic Gladiator) are refused by the server.
export async function createCharacter(connection: Connection, name: string, classIndex: number): Promise<CharacterCreated> {
  const result = await connection.request<CharacterCreated>('ClientToServer', 'CreateCharacter', { Name: name, Class: classIndex * 4 }, 'ServerToClient', 'CharacterCreationSuccessful');
  if (result.Success !== true) throw new Error(`The server didn't create ${name}: the name may be taken or not allowed, or the class isn't unlocked yet.`);
  return result;
}

export async function enterWorld(connection: Connection, characterName: string): Promise<CharacterInformation> {
  return connection.request<CharacterInformation>('ClientToServer', 'SelectCharacter', { Name: characterName }, 'ServerToClient', 'CharacterInformationExtended');
}
