import os
from typing import Dict, Any, Optional
from pydantic import BaseSettings, Field
import json

class Settings(BaseSettings):
    # Database settings
    database_url: str = Field(default="sqlite:///./banhammer.db", env="DATABASE_URL")
    
    # Redis settings
    redis_url: str = Field(default="redis://localhost:6379/0", env="REDIS_URL")
    redis_enabled: bool = Field(default=True, env="REDIS_ENABLED")
    
    # API settings
    api_title: str = Field(default="BanHammer Anti-Cheat API", env="API_TITLE")
    api_version: str = Field(default="1.0.0", env="API_VERSION")
    debug_mode: bool = Field(default=False, env="DEBUG_MODE")
    
    # Security settings
    secret_key: str = Field(default="your-secret-key-here", env="SECRET_KEY")
    rate_limiting_enabled: bool = Field(default=True, env="RATE_LIMITING_ENABLED")
    
    # Anti-cheat configuration
    default_rate_limits: Dict[str, Dict[str, int]] = Field(default={
        "reward_collection": {"max_per_minute": 10, "max_value_per_minute": 1000},
        "resource_gather": {"max_per_minute": 30, "max_value_per_minute": 500},
        "level_progress": {"max_per_minute": 3, "max_value_per_minute": 100},
        "purchase": {"max_per_minute": 5, "max_value_per_minute": 10000}
    })
    
    behavior_analysis_window: int = Field(default=300, env="BEHAVIOR_ANALYSIS_WINDOW")  # seconds
    anomaly_threshold: float = Field(default=2.5, env="ANOMALY_THRESHOLD")  # standard deviations
    auto_ban_threshold: float = Field(default=8.0, env="AUTO_BAN_THRESHOLD")
    
    # Bot detection settings
    perfect_timing_threshold: float = Field(default=0.05, env="PERFECT_TIMING_THRESHOLD")
    identical_sequences_threshold: int = Field(default=5, env="IDENTICAL_SEQUENCES_THRESHOLD")
    continuous_activity_hours: int = Field(default=3, env="CONTINUOUS_ACTIVITY_HOURS")
    
    # Logging settings
    log_level: str = Field(default="INFO", env="LOG_LEVEL")
    log_requests: bool = Field(default=True, env="LOG_REQUESTS")
    
    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"

# Global settings instance
settings = Settings()

class AntiCheatConfigManager:
    """Manages dynamic anti-cheat configuration that can be updated at runtime."""
    
    def __init__(self):
        self._config_cache = {}
        self._load_default_config()
    
    def _load_default_config(self):
        """Load default configuration values."""
        self._config_cache = {
            "rate_limits": settings.default_rate_limits,
            "behavior_analysis": {
                "window_seconds": settings.behavior_analysis_window,
                "anomaly_threshold": settings.anomaly_threshold,
                "perfect_timing_threshold": settings.perfect_timing_threshold,
                "identical_sequences_threshold": settings.identical_sequences_threshold,
                "continuous_activity_hours": settings.continuous_activity_hours
            },
            "auto_actions": {
                "auto_ban_enabled": True,
                "auto_ban_threshold": settings.auto_ban_threshold,
                "temp_ban_duration_hours": 24,
                "warning_threshold": 5.0
            },
            "monitoring": {
                "log_all_violations": True,
                "alert_on_high_risk": True,
                "cleanup_old_data_hours": 24 * 7  # 7 days
            }
        }
    
    def get(self, key: str, default: Any = None) -> Any:
        """Get configuration value by key."""
        keys = key.split('.')
        value = self._config_cache
        
        for k in keys:
            if isinstance(value, dict) and k in value:
                value = value[k]
            else:
                return default
        
        return value
    
    def set(self, key: str, value: Any) -> bool:
        """Set configuration value by key."""
        keys = key.split('.')
        config = self._config_cache
        
        # Navigate to the parent of the target key
        for k in keys[:-1]:
            if k not in config:
                config[k] = {}
            config = config[k]
        
        # Set the value
        config[keys[-1]] = value
        return True
    
    def get_rate_limit(self, action_type: str) -> Optional[Dict[str, int]]:
        """Get rate limit configuration for specific action type."""
        return self.get(f"rate_limits.{action_type}")
    
    def update_rate_limit(self, action_type: str, max_per_minute: int, max_value_per_minute: int):
        """Update rate limit for specific action type."""
        rate_limits = self.get("rate_limits", {})
        rate_limits[action_type] = {
            "max_per_minute": max_per_minute,
            "max_value_per_minute": max_value_per_minute
        }
        self.set("rate_limits", rate_limits)
    
    def get_all_config(self) -> Dict[str, Any]:
        """Get entire configuration."""
        return self._config_cache.copy()
    
    def load_from_db(self, db_configs: Dict[str, Any]):
        """Load configuration from database."""
        for key, value in db_configs.items():
            self.set(key, value)
    
    def export_config(self) -> str:
        """Export configuration as JSON string."""
        return json.dumps(self._config_cache, indent=2)
    
    def import_config(self, config_json: str) -> bool:
        """Import configuration from JSON string."""
        try:
            config = json.loads(config_json)
            self._config_cache.update(config)
            return True
        except json.JSONDecodeError:
            return False

# Global config manager instance
config_manager = AntiCheatConfigManager()

def get_config_manager() -> AntiCheatConfigManager:
    """Get the global configuration manager."""
    return config_manager