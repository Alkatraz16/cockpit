using Godot;
using System;
using System.Collections.Generic;

public partial class Fighters : CharacterBody2D {
	public const float Speed = 200.0f;
	public const float Acceleration = 1000.0f;
	public const float Deceleration = 1500.0f;
	public const float JumpVelocity = -250.0f;
	public const float JumpCutMultiplier = 0.5f;
	private const int HitStartFrame = 2;
	private const int HitEndFrame = 4;
	private const int AttackDamage = 10;

	private AnimatedSprite2D animatedSprite;
	private Area2D attackHitbox;
	private ColorRect hitboxVisual;
	
	private bool isAttacking = false;
	private float hitboxOffsetX;
	private readonly HashSet<Node> alreadyHit = new();
	private bool facingRight = true;
	
	public override void _Ready() {
		animatedSprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		attackHitbox = GetNode<Area2D>("AttackHitbox");
		hitboxOffsetX = Mathf.Abs(attackHitbox.Position.X);
		hitboxVisual = attackHitbox.GetNode<ColorRect>("HitboxVisual");

		attackHitbox.Monitoring = false;
		attackHitbox.AreaEntered += OnHitboxAreaEntered;
		animatedSprite.FrameChanged += OnFrameChanged;
		animatedSprite.AnimationFinished += OnAnimationFinished;
		hitboxVisual.Visible = false;

		SetFacing(true);
		animatedSprite.Play("idle");
	}
	
	public override void _PhysicsProcess(double delta) {
		Vector2 velocity = Velocity;

		// Gravity
		if (!IsOnFloor()) {
			velocity += GetGravity() * (float)delta;
		}
		
		Velocity = velocity;

		// Attack
		if (Input.IsActionJustPressed("attack") && !isAttacking) {
			StartAttack();
		}

		// Jump
		Jump();

		// Horizontal movement
		float direction = Input.GetAxis("move_left", "move_right");
		Vector2 dir = new Vector2(direction, 0);

		Walk(dir, (float)delta);
		MoveAndSlide();
	}

	private void Walk(Vector2 dir, float delta) {
		Vector2 velocity = Velocity;

		if (dir.X != 0) {
			SetFacing(dir.X > 0);
			velocity.X = Mathf.MoveToward(velocity.X, dir.X * Speed, Acceleration * delta);
		} else {
			velocity.X = Mathf.MoveToward(velocity.X, 0, Deceleration * delta);
		}

		Velocity = velocity;
	}

	private void Jump() {
		if (Input.IsActionJustPressed("jump") && IsOnFloor()) {
			Velocity = new Vector2(Velocity.X, JumpVelocity);
		}
		if (Input.IsActionJustReleased("jump") && Velocity.Y < 0) {
			Velocity = new Vector2(Velocity.X, Velocity.Y * JumpCutMultiplier);
		}
	}

	// Attack functions

	private void StartAttack() {
		isAttacking = true;
		alreadyHit.Clear();
		animatedSprite.Play("attack");
	}

	private void EndAttack() {
		isAttacking = false;
		attackHitbox.SetDeferred(Area2D.PropertyName.Monitoring, false);
		animatedSprite.Play("idle");
		hitboxVisual.Visible = false;
	}

	// Hitbox is only live on the damaging frames
	private void OnFrameChanged() {
		if (!isAttacking) return;
		bool active = animatedSprite.Frame >= HitStartFrame
				   && animatedSprite.Frame <= HitEndFrame;
		attackHitbox.SetDeferred(Area2D.PropertyName.Monitoring, active);
		hitboxVisual.Visible = active;
	}

	private void OnAnimationFinished() {
		if (isAttacking) EndAttack();
	}

	private void OnHitboxAreaEntered(Area2D area) {
		Node target = area.GetParent();
		if (target == this || alreadyHit.Contains(target)) return; // one hit per swing
		alreadyHit.Add(target);

		if (target.HasMethod("TakeDamage")) {
			target.Call("TakeDamage", AttackDamage);
		}
	}

	private void SetFacing(bool right) {
		facingRight = right;
		animatedSprite.FlipH = !right;

		attackHitbox.Position = new Vector2(
			right ? hitboxOffsetX : -hitboxOffsetX,
			attackHitbox.Position.Y
		);
	}
}
