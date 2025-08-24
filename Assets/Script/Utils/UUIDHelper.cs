using System;
using System.Security.Cryptography;
using System.Text;

namespace TapStar.Utils
{
	/// <summary>
	/// UUID 생성 및 버전별 유틸리티 클래스입니다.
	/// 지원 버전:
	/// • v4: 완전 무작위
	/// • v5: SHA-1 네임스페이스 기반 (이름(name) → 동일 결과)
	/// • v7: 타임스탬프 기반 (시간순 정렬, 낮은 충돌율)
	///
	/// 또한 v5 전용 네임스페이스 UUID를 미리 정의하고,
	/// 필요시 새로운 네임스페이스 UUID를 생성할 수 있습니다.
	/// </summary>
	public static class UUIDHelper
	{
		/// <summary>
		/// v4 UUID (완전 무작위) 생성
		/// </summary>
		public static Guid CreateUuid4() => Guid.NewGuid();

		/// <summary>
		/// UUID v5 생성 (SHA-1 네임스페이스 기반).
		/// 같은 namespaceId + name → 동일한 UUID를 리턴합니다.
		/// </summary>
		public static Guid CreateUuid5(Guid namespaceId, string name)
		{
			// 네임스페이스와 이름 문자열 결합
			byte[] nsBytes = namespaceId.ToByteArray();
			SwapByteOrder(nsBytes);
			byte[] nameBytes = Encoding.UTF8.GetBytes(name);

			// SHA1 해시
			byte[] hash;
			using (var sha1 = SHA1.Create())
			{
				sha1.TransformBlock(nsBytes, 0, nsBytes.Length, null, 0);
				sha1.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
				hash = sha1.Hash;
			}

			// 앞 16바이트를 새 GUID로
			byte[] newGuid = new byte[16];
			Array.Copy(hash, 0, newGuid, 0, 16);

			// 버전(5) / 변형 비트 설정
			newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
			newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

			SwapByteOrder(newGuid);
			return new Guid(newGuid);
		}

		/// <summary>
		/// UUID v7 생성 (타임스탬프+랜덤).
		/// 시간순 정렬이 가능하며, 충돌 위험이 낮습니다.
		/// </summary>
		public static Guid CreateUuid7()
		{
			// 1) 48비트 밀리초 타임스탬프
			ulong unixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			byte[] uuid = new byte[16];
			uuid[0] = (byte)(unixMs >> 40);
			uuid[1] = (byte)(unixMs >> 32);
			uuid[2] = (byte)(unixMs >> 24);
			uuid[3] = (byte)(unixMs >> 16);
			uuid[4] = (byte)(unixMs >> 8);
			uuid[5] = (byte)unixMs;

			// 2) 랜덤 74비트 생성
			byte[] rnd = new byte[10];
			RandomNumberGenerator.Fill(rnd);

			// 바이트6: 버전(7) + 상위 4비트 랜덤
			uuid[6] = (byte)((rnd[0] & 0x0F) | (7 << 4));
			// 바이트7: 하위 8비트 랜덤
			uuid[7] = rnd[1];
			// 바이트8: 변형 비트(10) + 상위 6비트 랜덤
			uuid[8] = (byte)((rnd[2] & 0x3F) | 0x80);
			// 바이트9~15: 나머지 랜덤
			Array.Copy(rnd, 3, uuid, 9, 7);

			// RFC4122 little-endian 변환
			SwapByteOrder(uuid);
			return new Guid(uuid);
		}

		/// <summary>
		/// UUID v7 스타일로 생성하되,
		/// string key를 HMAC-SHA256의 키로 사용해
		/// 랜덤 바이트를 추출합니다.
		/// 같은 key + 같은 타임스탬프 조합에 대해
		/// 항상 동일한 UUID를 반환합니다.
		/// </summary>
		/// <param name="key">임의 문자열 키</param>
		public static Guid CreateUuid7(string key)
		{
			// 1) 48비트 Unix 밀리초 시간
			ulong unixMs = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			byte[] timestamp = new byte[6];
			timestamp[0] = (byte)(unixMs >> 40);
			timestamp[1] = (byte)(unixMs >> 32);
			timestamp[2] = (byte)(unixMs >> 24);
			timestamp[3] = (byte)(unixMs >> 16);
			timestamp[4] = (byte)(unixMs >> 8);
			timestamp[5] = (byte)(unixMs);

			// 2) HMAC-SHA256(key, timestamp) → 해시값
			byte[] keyBytes = Encoding.UTF8.GetBytes(key);
			byte[] hash;
			using (var hmac = new HMACSHA256(keyBytes))
				hash = hmac.ComputeHash(timestamp);

			// 3) UUID 용 16바이트 버퍼 생성
			byte[] uuid = new byte[16];
			// 앞 6바이트: timestamp
			Array.Copy(timestamp, 0, uuid, 0, 6);

			// 바이트6: 상위 4비트=버전7, 하위 4비트=hash[6] 상위 4비트
			uuid[6] = (byte)((hash[6] & 0x0F) | (7 << 4));
			// 바이트7: hash[7]
			uuid[7] = hash[7];
			// 바이트8: 상위 2비트=10(variant), 하위 6비트=hash[8]&0x3F
			uuid[8] = (byte)((hash[8] & 0x3F) | 0x80);
			// 바이트9~15: hash[9..15]
			Array.Copy(hash, 9, uuid, 9, 7);

			// 4) RFC4122 little-endian ↔ big-endian 변환
			SwapByteOrder(uuid);
			return new Guid(uuid);
		}

		/// <summary>
		/// 새로운 네임스페이스 UUID(v4)를 생성합니다.
		/// v5용 네임스페이스로 쓰거나, 고정 네임스페이스로 사용하세요.
		/// </summary>
		public static Guid GenerateNamespaceId()
			=> Guid.NewGuid();

		/// <summary>
		/// Guid(byte[]) 생성 전후에 호출하여
		/// Data1~3 필드를 little/big endian 으로 바꿔줍니다.
		/// </summary>
		private static void SwapByteOrder(byte[] guid)
		{
			void Swap(int a, int b) { byte t = guid[a]; guid[a] = guid[b]; guid[b] = t; }
			Swap(0, 3); Swap(1, 2); Swap(4, 5); Swap(6, 7);
		}
	}
}
