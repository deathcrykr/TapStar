from sqlalchemy import Column, Integer, String, Float, DateTime, Text, Boolean, ForeignKey, Index
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import relationship
from sqlalchemy.sql import func
from datetime import datetime
import json

Base = declarative_base()

class Player(Base):
    __tablename__ = "players"
    
    id = Column(String, primary_key=True)
    username = Column(String(50), nullable=False)
    created_at = Column(DateTime, default=func.now())
    last_activity = Column(DateTime, default=func.now())
    risk_score = Column(Float, default=0.0)
    is_banned = Column(Boolean, default=False)
    ban_reason = Column(Text)
    ban_timestamp = Column(DateTime)
    
    # Relationships
    violations = relationship("Violation", back_populates="player", cascade="all, delete-orphan")
    actions = relationship("PlayerAction", back_populates="player", cascade="all, delete-orphan")

class PlayerAction(Base):
    __tablename__ = "player_actions"
    
    id = Column(Integer, primary_key=True)
    player_id = Column(String, ForeignKey("players.id"), nullable=False)
    action_type = Column(String(50), nullable=False)
    timestamp = Column(DateTime, default=func.now())
    value = Column(Float, default=0.0)
    metadata = Column(Text)  # JSON string
    
    # Relationships
    player = relationship("Player", back_populates="actions")
    
    # Indexes for performance
    __table_args__ = (
        Index('idx_player_timestamp', 'player_id', 'timestamp'),
        Index('idx_action_type', 'action_type'),
        Index('idx_timestamp', 'timestamp'),
    )
    
    def get_metadata(self):
        if self.metadata:
            return json.loads(self.metadata)
        return {}
    
    def set_metadata(self, data):
        self.metadata = json.dumps(data) if data else None

class Violation(Base):
    __tablename__ = "violations"
    
    id = Column(Integer, primary_key=True)
    player_id = Column(String, ForeignKey("players.id"), nullable=False)
    violation_type = Column(String(50), nullable=False)
    severity = Column(Float, nullable=False)
    timestamp = Column(DateTime, default=func.now())
    details = Column(Text)  # JSON string
    resolved = Column(Boolean, default=False)
    reviewed_by = Column(String(50))  # Admin who reviewed
    review_timestamp = Column(DateTime)
    
    # Relationships
    player = relationship("Player", back_populates="violations")
    
    # Indexes
    __table_args__ = (
        Index('idx_player_violation', 'player_id', 'timestamp'),
        Index('idx_violation_type', 'violation_type'),
        Index('idx_severity', 'severity'),
        Index('idx_unresolved', 'resolved', 'timestamp'),
    )
    
    def get_details(self):
        if self.details:
            return json.loads(self.details)
        return {}
    
    def set_details(self, data):
        self.details = json.dumps(data) if data else None

class BanHistory(Base):
    __tablename__ = "ban_history"
    
    id = Column(Integer, primary_key=True)
    player_id = Column(String, ForeignKey("players.id"), nullable=False)
    ban_type = Column(String(20), nullable=False)  # temporary, permanent
    ban_reason = Column(Text, nullable=False)
    banned_by = Column(String(50))  # Admin or system
    ban_timestamp = Column(DateTime, default=func.now())
    unban_timestamp = Column(DateTime)  # For temporary bans
    is_active = Column(Boolean, default=True)
    
    # Indexes
    __table_args__ = (
        Index('idx_player_ban', 'player_id', 'is_active'),
        Index('idx_ban_timestamp', 'ban_timestamp'),
    )

class AntiCheatConfig(Base):
    __tablename__ = "anticheat_config"
    
    id = Column(Integer, primary_key=True)
    config_key = Column(String(100), unique=True, nullable=False)
    config_value = Column(Text, nullable=False)
    description = Column(Text)
    updated_at = Column(DateTime, default=func.now(), onupdate=func.now())
    updated_by = Column(String(50))
    
    def get_value(self):
        try:
            return json.loads(self.config_value)
        except json.JSONDecodeError:
            return self.config_value
    
    def set_value(self, value):
        if isinstance(value, (dict, list)):
            self.config_value = json.dumps(value)
        else:
            self.config_value = str(value)

class PlayerStats(Base):
    __tablename__ = "player_stats"
    
    id = Column(Integer, primary_key=True)
    player_id = Column(String, ForeignKey("players.id"), nullable=False, unique=True)
    total_actions = Column(Integer, default=0)
    avg_session_duration = Column(Float, default=0.0)  # minutes
    total_violations = Column(Integer, default=0)
    highest_risk_score = Column(Float, default=0.0)
    last_violation_timestamp = Column(DateTime)
    suspicious_patterns = Column(Text)  # JSON array of detected patterns
    
    # Behavioral metrics
    action_frequency_score = Column(Float, default=0.0)  # Actions per minute average
    timing_variance_score = Column(Float, default=0.0)  # How varied timing is
    value_consistency_score = Column(Float, default=0.0)  # How consistent values are
    
    # Timestamps
    first_seen = Column(DateTime, default=func.now())
    last_updated = Column(DateTime, default=func.now(), onupdate=func.now())
    
    def get_suspicious_patterns(self):
        if self.suspicious_patterns:
            return json.loads(self.suspicious_patterns)
        return []
    
    def set_suspicious_patterns(self, patterns):
        self.suspicious_patterns = json.dumps(patterns) if patterns else None