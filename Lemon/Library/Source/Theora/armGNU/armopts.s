@********************************************************************
@*                                                                  *
@* THIS FILE IS PART OF THE OggTheora SOFTWARE CODEC SOURCE CODE.   *
@* USE, DISTRIBUTION AND REPRODUCTION OF THIS LIBRARY SOURCE IS     *
@* GOVERNED BY A BSD-STYLE SOURCE LICENSE INCLUDED WITH THIS SOURCE *
@* IN 'COPYING'. PLEASE READ THESE TERMS BEFORE DISTRIBUTING.       *
@*                                                                  *
@* THE Theora SOURCE CODE IS COPYRIGHT (C) 2002-2010                *
@* by the Xiph.Org Foundation and contributors http://www.xiph.org/ *
@*                                                                  *
@********************************************************************
@ Original implementation:
@  Copyright (C) 2009 Robin Watts for Pinknoise Productions Ltd
@ last mod: $Id: armopts.s.in 17430 2010-09-22 21:54:09Z tterribe $
@********************************************************************

@ Set the following to 1 if we have EDSP instructions
@  (LDRD/STRD, etc., ARMv5E and later).
 .set OC_ARM_ASM_EDSP,	1 @,:HAVE_ARM_ASM_EDSP@

@ Set the following to 1 if we have ARMv6 media instructions.
 .set OC_ARM_ASM_MEDIA,	1 @,:HAVE_ARM_ASM_MEDIA@

@ Set the following to 1 if we have NEON (some ARMv7)
 .set OC_ARM_ASM_NEON,	0 @,:HAVE_ARM_ASM_NEON@

@ Set the following to 1 if LDR/STR can work on unaligned addresses
@ This is assumed to be true for ARMv6 and later code
 .set OC_ARM_CAN_UNALIGN,	1

@ Large unaligned loads and stores are often configured to cause an exception.
@ They cause an 8 cycle stall when they cross a 128-bit (load) or 64-bit (store)
@  boundary, so it's usually a bad idea to use them anyway if they can be
@  avoided.

@ Set the following to 1 if LDRD/STRD can work on unaligned addresses
 .set OC_ARM_CAN_UNALIGN_LDRD,	0

	@ END
