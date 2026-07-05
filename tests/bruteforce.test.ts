import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// Giả lập hệ thống chống Brute Force (tương tự như trong main.ts)
class BruteForceProtector {
  private config: any = { 
    bruteForceAttempts: 0,
    lockoutUntil: 0
  };

  constructor() {}

  async attemptLogin(success: boolean) {
    const now = Date.now();

    if (this.config.lockoutUntil && now < this.config.lockoutUntil) {
      const remainingMinutes = Math.ceil((this.config.lockoutUntil - now) / 60000);
      throw new Error(`Két sắt đã bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau ${remainingMinutes} phút.`);
    }

    if (this.config.lockoutUntil && now >= this.config.lockoutUntil) {
      this.config.lockoutUntil = 0;
      this.config.bruteForceAttempts = 0;
    }

    if (!success) {
      this.config.bruteForceAttempts = (this.config.bruteForceAttempts || 0) + 1;
      if (this.config.bruteForceAttempts >= 5) {
        this.config.lockoutUntil = now + 5 * 60 * 1000; // Khóa 5 phút
        throw new Error('Đăng nhập sai 5 lần. Két sắt bị khóa trong 5 phút.');
      }
      return false; // Sai nhưng chưa bị khóa
    }

    // Đăng nhập thành công thì reset
    this.config.bruteForceAttempts = 0;
    this.config.lockoutUntil = 0;
    return true;
  }

  getConfig() {
    return this.config;
  }
}

describe('Hệ thống Chống Brute Force', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('Cho phép đăng nhập đúng và reset bộ đếm', async () => {
    const protector = new BruteForceProtector();
    await protector.attemptLogin(false); // Sai 1 lần
    expect(protector.getConfig().bruteForceAttempts).toBe(1);
    
    await protector.attemptLogin(true); // Đúng
    expect(protector.getConfig().bruteForceAttempts).toBe(0);
  });

  it('Khóa hệ thống 5 phút sau 5 lần sai liên tiếp', async () => {
    const protector = new BruteForceProtector();
    
    await protector.attemptLogin(false); // 1
    await protector.attemptLogin(false); // 2
    await protector.attemptLogin(false); // 3
    await protector.attemptLogin(false); // 4
    
    await expect(protector.attemptLogin(false)).rejects.toThrow('Đăng nhập sai 5 lần'); // 5
    expect(protector.getConfig().lockoutUntil).toBeGreaterThan(Date.now());
  });

  it('Phải chặn đăng nhập khi đang trong thời gian khóa', async () => {
    const protector = new BruteForceProtector();
    
    // Simulate bị khóa
    for(let i=0; i<4; i++) await protector.attemptLogin(false);
    await expect(protector.attemptLogin(false)).rejects.toThrow();

    // Thử đăng nhập lại (kể cả đúng pass) vẫn phải bị chặn
    await expect(protector.attemptLogin(true)).rejects.toThrow(/Két sắt đã bị khóa/);
  });

  it('Mở khóa sau khi hết 5 phút', async () => {
    const protector = new BruteForceProtector();
    
    for(let i=0; i<4; i++) await protector.attemptLogin(false);
    await expect(protector.attemptLogin(false)).rejects.toThrow();

    // Tua nhanh thời gian 6 phút
    vi.advanceTimersByTime(6 * 60 * 1000);

    // Đăng nhập lại bình thường
    const res = await protector.attemptLogin(true);
    expect(res).toBe(true);
    expect(protector.getConfig().bruteForceAttempts).toBe(0);
    expect(protector.getConfig().lockoutUntil).toBe(0);
  });
});
